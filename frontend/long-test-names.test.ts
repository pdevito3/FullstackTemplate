import { spawnSync } from 'node:child_process'
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { expect, test } from 'vitest'

interface JsonReport {
  testResults: {
    message: string
    assertionResults: { title: string; status: string; failureMessages: string[] }[]
  }[]
}

const attachments = join(import.meta.dirname, 'node_modules/.vite-long-test-names/attachments')

function childEnv() {
  return Object.fromEntries(Object.entries(process.env).filter(([name]) => !name.startsWith('VITEST')))
}

// CI=1 records a Playwright trace of each component test, and Vitest names the trace files and the failure screenshot
// from the test name (#479). The fixture is in a folder with a long path, because the name of the copy of a trace has
// the path of the test file in it.
test('a component test file with test names longer than a file name reports each test, and keeps the trace and the screenshot', { timeout: 180_000 }, () => {
  const dir = mkdtempSync(join(tmpdir(), 'long-test-names-'))
  try {
    const report = join(dir, 'report.json')
    rmSync(attachments, { recursive: true, force: true })
    const run = spawnSync(
      'pnpm',
      ['exec', 'vitest', 'run', '--config', 'long-test-names.config.ts', '--reporter=default', '--reporter=json', `--outputFile=${report}`],
      // spawnSync blocks the event loop, so the timeout of the test cannot stop a child that hangs.
      { cwd: import.meta.dirname, env: childEnv(), encoding: 'utf8', timeout: 150_000 },
    )
    const output = `${run.stdout}${run.stderr}`
    expect(run.error, output).toBeUndefined()
    const [file] = (JSON.parse(readFileSync(report, 'utf8')) as JsonReport).testResults

    expect(output).not.toContain('ENAMETOOLONG')
    expect(file.message).toBe('')
    expect(file.assertionResults.map(({ title, status }) => [title.slice(0, 10), status])).toEqual([
      ['fails and ', 'failed'],
      ['passes and', 'passed'],
      ['runs after', 'passed'],
    ])
    expect(file.assertionResults[0].failureMessages.join()).toContain('expected 1 to be 2')
    const kept = readdirSync(attachments, { recursive: true, encoding: 'utf8' })
    expect(kept.filter((path) => path.endsWith('.zip'))).toHaveLength(1)
    expect(kept.filter((path) => path.startsWith('failure-screenshots') && path.endsWith('.png'))).toHaveLength(1)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
