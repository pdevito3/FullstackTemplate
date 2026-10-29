# Spec: <the domain>

Copy this file to `docs/specs/<domain>.md` and replace each part in angle brackets. Keep the sections in this order. Write "None" in a section that has nothing, so that a reader knows that you did not forget it. The rows in this file are examples of a work order domain.

## 1. Scope

<What the app does in this spec, who uses it, and what it leaves out. Link the issue or the map that gives the reasons.>

## 2. Entities and states

<Each aggregate root, its children, and its value objects. For each entity with a status, give the states and the moves between them.>

| Entity | Root of | Fields that a user sets | States |
|---|---|---|---|
| `WorkOrder` | `WorkOrderPart` | title, due date | Open, Closed |

| From | To | Method | Who |
|---|---|---|---|
| Open | Closed | `WorkOrder.Close` | A user with `manage_work_orders` |

## 3. Rules

Each rule has an ID in the first cell of its row. Use one prefix for each spec, such as `WO` for work orders, so that two specs cannot give the same ID. The examples use `R`. Do not change the ID of a rule or use it again for another rule. When you remove a rule, remove its row.

Mark a rule `(later)` after its ID until a slice builds it. A rule without `(later)` must have a test that names its ID:

- A .NET test: `[Property("Rule", "R-1")]` on the `[Test]` method.
- A Playwright test: the tag `@R-1`, as in `test('...', { tag: '@R-1' }, ...)`, or `@R-1` in the title or the annotation of the test.
- A Vitest test: `@R-1` in the name of the test.

`SpecRuleTests` fails when a rule has no test, when a test names an ID that no spec defines, when a `(later)` rule has a test, when two rows have the same ID, or when a row of a rule table gives no ID. It reads each table whose first column is `ID` as a rule table, so give a table of other items, such as open decisions, another first column. It also fails when it cannot read the Rules section: when the section has no rule table, when a table in the section has another first column (such as `Rule ID`), or when a list item in the section starts with a rule ID. Write "None" in a Rules section that has no rules. It reads each spec in `docs/specs/` and its subfolders, but not a table in a code block. To list the faults, run this command from the repository root:

```bash
dotnet test --project tests/FullstackTemplate.UnitTests -- --treenode-filter "/*/*/SpecRuleTests/*"
```

Only a test that runs counts. In .NET, that is a `[Test]` method with no `[Skip]` on it or on its class, and the `[Property]` has the string literals `"Rule"` and the ID. In the frontend, that is a `test(...)` or `it(...)` call with a string title, and the check reads only its title and its options object (the `tag` and the `annotation`). These do not count:

- A rule ID in a comment, in another string, or on a helper method.
- `test.skip`, `test.fixme`, a test with `skip: true` or `todo: true` in its options, and a test in `describe.skip`, `describe.fixme`, or `describe.todo`.
- The tag or title of `test.describe`, `test.each`, `it.each`, a title in a variable, and a `[Property]` on a class or with a `const`. Put the ID on the test itself.

The check finds the Rules section by a heading with the word "rules", such as `## 3. Rules`.

To run the .NET tests of one rule, give the filter to each test project that has them:

```bash
dotnet test --project tests/FullstackTemplate.UnitTests -- --treenode-filter "/*/*/*/*[Rule=R-1]"
```

To run the frontend tests of one rule, run `pnpm test:e2e --grep "@R-1\b"`, `pnpm test:e2e:stack --grep "@R-1\b"`, or `pnpm test -t "@R-1\b"` in `frontend/`. The `\b` keeps out `@R-10`.

| ID | Rule | Enforced by | Backstop |
|---|---|---|---|
| R-1 | A work order has a title of 1 to 200 characters. | `WorkOrder.Create`, `WorkOrder.Update` | |
| R-2 | Two work orders of a tenant do not have the same title. | Handler check, `ConflictException` (409) | Unique index on `work_orders.tenant_id, title` |
| R-3 (later) | A closed work order does not open again. | `WorkOrder.Reopen` | |

"Enforced by" names the method or the handler check that refuses the change. "Backstop" names the database rule that stops a race between two requests (see [concurrency.md](../backend/concurrency.md)).

## 4. Permissions

| Permission | Words in a message | What it allows | Default roles |
|---|---|---|---|
| `manage_work_orders` | manage work orders | Create, edit, and close a work order | Admin |

The words are the text of a denial (see [messages.md](../messages.md)).

## 5. Endpoints and tools

| Feature | HTTP | gRPC | MCP tool | Permission |
|---|---|---|---|---|
| `AddWorkOrder` | `POST /api/v1/work-orders` | | `add_work_order` | `manage_work_orders` |

Add only the entrypoints that the callers of a feature need (see [endpoints.md](../backend/endpoints.md)).

## 6. Messages

<The text of each message that a user can read, and the rule that gives it. Follow [messages.md](../messages.md).>

| Rule | Status | Message |
|---|---|---|
| R-2 | 409 | A work order with this title already exists. |

## 7. Time zones

<Each date and time of the domain, the zone that it is in, and the place that gives the zone. Write "None" when the domain has no dates.>

| Value | Stored as | Shown in the zone of |
|---|---|---|
| Due date | `date` | The site of the work order |

## 8. Open decisions

<Each question that the spec does not answer yet, who decides it, and the rules that wait on it.>
