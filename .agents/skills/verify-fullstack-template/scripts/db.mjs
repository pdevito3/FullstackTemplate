// A row is known by the JSON of its primary key, so a restore deletes and inserts only the rows that the recipe
// added, changed, or deleted. The other rows keep their xmin, which is the Version of an entity.
import { execFileSync } from 'node:child_process'

const ident = (name) => `"${name.replaceAll('"', '""')}"`
const literal = (value) => `'${value.replaceAll("'", "''")}'`
const qualified = (table) => `${ident(table.schema)}.${ident(table.name)}`
const keyExpression = (table, alias) => `json_build_array(${table.key.map((column) => `${alias}.${ident(column)}`).join(', ')})::text`

export function postgresContainer(stack) {
  const described = JSON.parse(
    execFileSync('dotnet', ['aspire', 'describe', 'postgres', '--apphost', stack.appHostPath, '--format', 'Json', '--nologo',
      '--non-interactive'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }),
  )
  const id = described.resources?.[0]?.properties?.['container.id']
  if (!id) throw new Error('db: aspire describe postgres gave no container.id. Is the stack running?')
  return id
}

// The output of a copy has no size limit, so the buffer has none.
function psql(container, database, sql) {
  try {
    return execFileSync('docker', ['exec', '-i', container, 'sh', '-c',
      `PGPASSWORD="$POSTGRES_PASSWORD" psql -U postgres -d ${database} -v ON_ERROR_STOP=1 -qAt`],
    { input: sql, encoding: 'utf8', maxBuffer: Infinity, stdio: ['pipe', 'pipe', 'pipe'] }).trim()
  } catch (e) {
    throw new Error(`db: psql on ${database} failed: ${e.stderr?.toString().trim() || e.message}`, { cause: e })
  }
}

const databases = (container) =>
  psql(container, 'postgres', `SELECT datname FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres' ORDER BY 1;`)
    .split('\n').filter(Boolean)

function tablesOf(container, database) {
  const json = psql(container, database, `
    SELECT coalesce(json_agg(json_build_object(
      'schema', n.nspname,
      'name', c.relname,
      'key', (SELECT json_agg(a.attname ORDER BY k.ord) FROM pg_index i
                CROSS JOIN unnest(i.indkey) WITH ORDINALITY k(attnum, ord)
                JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum
                WHERE i.indrelid = c.oid AND i.indisprimary),
      'columns', (SELECT json_agg(a.attname ORDER BY a.attnum) FROM pg_attribute a
                    WHERE a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped AND a.attgenerated = '')
    ) ORDER BY n.nspname, c.relname), '[]')
    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%';`)
  const tables = JSON.parse(json)
  const keyless = tables.filter((table) => !table.key)
  if (keyless.length) {
    throw new Error(`db: ${database} has tables with no primary key, so a restore cannot find their rows: ${keyless.map(qualified).join(', ')}`)
  }
  return tables
}

// JSON escapes each tab and line break in a value, so a row is one line with two tabs.
function rowsOf(container, database, tables) {
  const rows = Object.fromEntries(tables.map((table) => [qualified(table), {}]))
  if (!tables.length) return rows
  const sql = tables
    .map((table, i) => `SELECT '${i}' || E'\\t' || ${keyExpression(table, 't')} || E'\\t' || row_to_json(t)::text FROM ${qualified(table)} t`)
    .join('\nUNION ALL\n') + ';'
  for (const line of psql(container, database, sql).split('\n').filter(Boolean)) {
    const [i, key, row] = line.split('\t')
    rows[qualified(tables[i])][key] = row
  }
  return rows
}

export function snapshot(container) {
  return Object.fromEntries(databases(container).map((database) => {
    const tables = tablesOf(container, database)
    return [database, { tables, rows: rowsOf(container, database, tables) }]
  }))
}

function differences(before, after) {
  const diffs = []
  for (const [database, { tables, rows }] of Object.entries(before)) {
    for (const table of tables) {
      const was = rows[qualified(table)]
      const now = after[database]?.rows[qualified(table)] ?? {}
      const added = Object.keys(now).filter((key) => !(key in was))
      const changed = Object.keys(was).filter((key) => key in now && now[key] !== was[key])
      const deleted = Object.keys(was).filter((key) => !(key in now))
      if (added.length + changed.length + deleted.length) diffs.push({ database, table, added, changed, deleted })
    }
  }
  return diffs
}

// The replica role turns off the triggers, so the foreign keys do not check the order of the statements and no
// cascade runs. The end state is the snapshot, which was consistent.
function restoreSql(before, diffs) {
  const byDatabase = Map.groupBy(diffs, (diff) => diff.database)
  return new Map([...byDatabase].map(([database, tableDiffs]) => {
    const statements = tableDiffs.flatMap(({ table, added, changed, deleted }) => {
      const remove = [...added, ...changed]
      // The row text goes back as Postgres wrote it. A JSON.parse would turn 10.50 into 10.5 and round a large bigint.
      const insert = [...changed, ...deleted].map((key) => before[database].rows[qualified(table)][key])
      const columns = table.columns.map(ident).join(', ')
      return [
        remove.length && `DELETE FROM ${qualified(table)} t WHERE ${keyExpression(table, 't')} IN (${remove.map(literal).join(', ')});`,
        insert.length && `INSERT INTO ${qualified(table)} (${columns}) OVERRIDING SYSTEM VALUE SELECT ${columns} FROM json_populate_recordset(NULL::${qualified(table)}, ${literal(`[${insert.join(',')}]`)});`,
      ].filter(Boolean)
    })
    return [database, `BEGIN;\nSET LOCAL session_replication_role = replica;\n${statements.join('\n')}\nCOMMIT;`]
  }))
}

const changeCountsByTable = (diffs) =>
  Object.fromEntries(diffs.map(({ database, table, added, changed, deleted }) =>
    [`${database}.${table.schema}.${table.name}`, { added: added.length, changed: changed.length, deleted: deleted.length }]))

// A failed restore gives the rows of the run in `error.changes`, so that the report shows what stayed in the database.
export function restore(container, before) {
  const diffs = differences(before, snapshot(container))
  const changes = changeCountsByTable(diffs)
  try {
    for (const [database, sql] of restoreSql(before, diffs)) psql(container, database, sql)
    const left = differences(before, snapshot(container))
    if (left.length) {
      throw new Error(`db: the restore left these tables different from the snapshot: ${Object.keys(changeCountsByTable(left)).join(', ')}`)
    }
  } catch (e) {
    throw Object.assign(e, { changes })
  }
  return changes
}
