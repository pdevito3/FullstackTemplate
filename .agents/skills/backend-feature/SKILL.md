---
name: backend-feature
description: Scaffolds backend features including new domain entities, new properties on existing entities, and new business operations. Use when creating a new domain entity, extending an existing entity with new properties, or adding new business features/commands.
allowed-tools: Read, Glob, Grep, Write, Edit, Bash(dotnet:*, ls:*, mkdir:*, pnpm:*)
---

# Backend Feature

Three tasks:

1. A new entity: run `scripts/new-feature.cs`, then write the domain rules.
2. A new property on an entity that exists.
3. A new business operation on an entity that exists.

The files that the script writes are the reference for each pattern of a CRUD feature.
<!-- example:start -->
The template also has a reference feature in `FullstackTemplate.Server/Examples/Workshops` and `Examples/Venues`. It shows each core pattern: a decision shape, a run again, a version, allowed actions, and the three entrypoints. `CancelWorkshop` and `ScheduleWorkshop` show each step of a new operation.
<!-- example:end -->

Read the rule doc of each area in `docs/backend/` before you change it.

## 1. New Entity

### Run the script

1. Ask the user for the properties and for the entrypoints of the entity: HTTP, gRPC, MCP, or none.
2. Run the script from the root of the repo:

   ```bash
   dotnet run scripts/new-feature.cs -- SupplyItem --entrypoints http,mcp \
     --fields "Name:text, Notes:text?, Code:text:public, Quantity:int, IsActive:bool, ReceivedOn:date, CountedAt:instant"
   dotnet run scripts/new-feature.cs -- Delivery --entrypoints http,mcp \
     --fields "Title:text, SupplyItem:ref, DueOn:zoned-date"
   ```

   Run it with no arguments to see the options. The first property must be required text: the list searches and sorts by it. A text, date, zoned date, or instant value is `[Sensitive]`. Add `:public` when the value cannot hold personal data. Protobuf has no date type, so the script refuses `date` and `zoned-date` with `grpc`.

   - `zoned-date` is a day at the place of the entity, such as a due date. The script adds the property `TimeZone`, an IANA ID that the add sets. To get the current day at the place, use `ZonedDate.TodayAt(TimeZone, now)`, not the date of the server.
   - `SupplyItem:ref` is the property `SupplyItemId`: the ID of a `SupplyItem` of the same tenant. Add the parent with the script first. The add loads the parent and refuses an ID that the caller cannot see. An update does not change the reference. An entity has at most one `ref`.

The script writes a tenant-owned root with add, get, list, update, and delete. It adds:

- The entity, the domain events, the models, the DTOs, the mapper, and the EF configuration.
- The five features and their entrypoints. There is no MCP delete tool, because a tool returns the DTO.
- The permission `manage_<plural>`, and a migration that gives it to each admin.
- The entity graph blueprint, the rules of the fakes, and the unit, integration, and functional tests.
- Lines in the shared files, at each `// new-feature:<slot>` marker: the `DbSet`, the permission, the gRPC map, `PermissionTests`, `StringColumnLengthTests`, `TenantDeleteOwnedRowsTests`, and `EndpointAccessSweepTests`.

A second run with the same input changes nothing. When a file that the script writes exists with other content, the script stops before it writes. CI runs the script for two samples on each pull request, and then builds and tests them (the `new-feature` job).

### Write the domain rules

The script writes generic rules. Make them the rules of the domain:

1. In the entity, set each max length, minimum, and maximum. The script writes each message from the names of the entity and the property, for example `Please give the supply item a value for "Received on".` Rewrite a message when a user needs other words to understand it (`docs/messages.md`). Update the messages in the unit tests of the entity to match.
2. Check each `[Sensitive]` and `[NotSensitive]` mark (`docs/backend/audit-log.md`).
3. Make each value that a person names into a value object, such as `EmailAddress` (`docs/backend/domain-entities.md`).
4. Add the guards and the business methods. For a rule that two callers share, or a rule that reads more than one aggregate, use the decision shape (`docs/backend/features-and-cqrs.md#one-rule-set-for-two-callers`).
5. Make the rules in `SharedTestHelpers/Fakes/<Plural>/<Entity>Rules.cs` give realistic values.
6. If a unique value, a second foreign key, or a child entity is necessary, change the configuration and add a migration (`docs/backend/entity-configurations.md`).
7. Regenerate and commit the API contract with the `api-contract` skill.
8. Run `dotnet build FullstackTemplate.slnx` and `dotnet test --solution FullstackTemplate.slnx`.

The script writes only a root that a tenant owns. For a child entity, an entity of all tenants, or a read model of a view, write the files by hand. Use the files of a generated feature as the pattern, and the checklist of `docs/backend/audit-log.md`.

## 2. New Property

Change each of these files:

| File | Change |
|------|--------|
| `<Entity>.cs` | The property with a private setter, its max length or range constants, and its check in `Validate<Entity>` |
| `Models/<Entity>ForCreation.cs`, `<Entity>ForUpdate.cs` | The property, if it is set at creation or if it can change |
| `Dtos/<Entity>Dto.cs`, `<Entity>ForCreationDto.cs`, `<Entity>ForUpdateDto.cs` | The property. A text value has `[SchemaMaxLength]` |
| `Mappings/<Entity>Mapper.cs` | A map for a value object |
| `Databases/EntityConfigurations/<Entity>Configuration.cs` | `HasMaxLength`, or `ComplexProperty` for a value object |
| Each MCP tool and gRPC method that makes a creation or an update DTO | A parameter or a field for the property |
| `SharedTestHelpers/EntityGraphs/<Entity>Input.cs`, `<Entity>Blueprints.cs`, and `<Entity>Rules` | The input property, the factory value, and a `Derive` for a required value |
| `StringColumnLengthTests` | Each new text column, in `CallerColumns`, `FixedOptionColumns`, or `ServerWrittenColumns` |

Rules:

- Each text, date, or time value has `[Sensitive]` or `[NotSensitive]`. A flag, an enum, a `Guid`, an integer, or a foreign key needs no mark.
- Check a required text value with `ThrowWhenNullOrWhitespace` first. Check each text value with `ThrowWhenLongerThan` and `ThrowWhenHasCharacterPostgresCannotStore`. Put the checks of a nullable value in an `is not null` block.
- A new required parameter of an MCP tool that exists needs a new tool name. To keep the name, add an optional parameter with the default `null` after `CancellationToken` ("MCP versions" in `docs/backend/endpoints.md`).
- A gRPC field that can have no value is `optional`. Read `Has<Field>`, and set the response field only when the value is not null.
- Give each new text value a rule in `<Entity>Rules` that fits the max length. Otherwise `AutoFaker` makes text that is too long, and unrelated tests fail at random.
- Add a migration: `dotnet tool exec dotnet-ef@<version> --yes -- migrations add Add<Property>To<Entity> --project FullstackTemplate.Server --startup-project FullstackTemplate.Server --output-dir Databases/Migrations`. The version is the `Microsoft.EntityFrameworkCore.Design` version in `Directory.Packages.props`.
- Regenerate and commit the API contract with the `api-contract` skill.

## 3. New Operation

1. Choose where the rule lives. Use the decision shape when a second caller needs the same rules, or when a rule reads more than one aggregate. Otherwise, use a guard on the entity.
2. Add the method to the entity. Queue a domain event with a name in the past tense. The event carries IDs and values, never an entity (`docs/backend/domain-events.md`).
3. Pick the concurrency tool from `docs/backend/concurrency.md`: a version, a run again (`IRunsAgainWhenRaced`), or a lock.
4. Write the command in `Features/<Action><Entity>/<Action><Entity>.cs`. Make the checks in the order of `docs/backend/features-and-cqrs.md#the-order-of-the-checks`. The first statement is the permission check with `IPermissionGuard`.
5. If the SPA shows a button for the action, add the action to the action value object of the entity, and call `GuardAllows` after the load (`docs/backend/domain-entities.md#allowed-actions`).
6. Add one adapter file for each entrypoint that the callers need (`docs/backend/endpoints.md`). An MCP tool has exactly one `[ToolAccess]`, which matches the check of the handler.
7. Add the telemetry with the `feature-telemetry` skill.
8. Add the tests with the `writing-tests` skill. A command that runs again or takes a lock has a race test. `EndpointAccessSweepTests` must pass for each new endpoint, tool, and gRPC method.
9. Regenerate and commit the API contract with the `api-contract` skill.

A new permission needs a `Permission.Names` constant, a `PermissionEnum` entry, a factory method, its name in `PermissionTests`, and a migration that gives it to the admins. The script writes these lines for a new entity. Use them as the pattern.

## Build rules

The build fails on these rules:

- Each entity has `public const string IdPrefix` with 1 to 17 lower-case ASCII letters. The prefix is unique. Each constructor calls `base(IdPrefix)` with the IdPrefix of its own type (FSTID001-005).
- An entity derives from `BaseEntity`, or from an abstract base class with no IdPrefix. The constructor of the abstract class takes the parameter `idPrefix` and gives it to `BaseEntity`.
- The text of a user message obeys `docs/messages.md` (the user message analyzer).
- A set-based write, raw SQL, or a bulk import uses the paths of `docs/backend/audit-log.md` (RS0030).
