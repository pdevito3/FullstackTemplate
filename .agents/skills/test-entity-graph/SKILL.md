---
name: test-entity-graph
description: Arranges test data with the entity graph builder in tests/FullstackTemplate.SharedTestHelpers/EntityGraphs. Use when writing the Arrange step of a unit or integration test that needs domain entities, or when adding a new entity, input, and blueprint to the entity graph.
---

# Test Entity Graph

The entity graph builder builds a graph of real domain entities for a test. Each `AddX` call runs the domain factory at once. `BuildAsync` saves all new entities in one save.

The code is in `tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/`:

| File | Content |
|---|---|
| `Core/` | The generic engine. MIT licensed, see `Core/LICENSE`. Do not add app types here. |
| `TestSeed.cs` | The seed: one tenant and the current user in that tenant. |
| `Inputs.cs` | One settable input class per entity. |
| `Blueprints.cs` | One blueprint per entity. The blueprint maps the input to the domain factory. The class has `[GraphGraft]`. |
| `TestGraph.cs` | The entry point and the shared `GraphOptions`. |

The source generator in `tests/FullstackTemplate.EntityGraphs.Generators/` reads `Blueprints.cs`. It makes the typed `AddX` and `AddXs` methods in the generated class `BlueprintsGrafts`. Do not write these methods by hand.

## Pick the right tool

| You need | Use |
|---|---|
| Entities that exist before the code under test runs | The entity graph |
| The request DTO for a command or an endpoint | The shape fakers in `Fakes/` (for example `FakeUserForCreationDto`) |
| The model for a direct domain factory test | The shape fakers in `Fakes/` (for example `FakeUserForCreation`) |

Do not send a create command (for example `AddUser.Command`) to arrange data. Use the entity graph. Send the command only when the command is the code under test.

## Arrange data in an integration test

1. Start the scenario with `testingServiceScope.StartGraph(out var seed)`.
2. Add entities with the `AddX` methods. Set only the values that the test asserts on.
3. Call `BuildAsync()`. Without this call, nothing goes to the database.

```csharp
var testingServiceScope = new TestingServiceScope();
await testingServiceScope.StartGraph(out _)
    .AddUser(out var user, u => u.Role = "Admin")
    .BuildAsync();

var result = await testingServiceScope.SendAsync(new GetUser.Query(user.Id));
```

The builder starts anchored to the seed tenant. As a result, `AddUser` right after `StartGraph` creates a user in the tenant of the current user. The seed rows are already in the database, and `BuildAsync` does not save them again.

`BuildAsync` clears the EF change tracker after the save. The code under test then loads the entities fresh from the database.

## Arrange data in a unit test

Use `TestGraph.Start(out var seed)`. This generates a new in-memory seed. You do not need to call `BuildAsync`, because the entities are complete after each `AddX` call.

```csharp
TestGraph.Start(out var seed)
    .AddUser(out var user, u => u.FirstName = "Ada");

user.TenantId.ShouldBe(seed.Tenant.Id);
```

## The AddX methods

| Call | Result |
|---|---|
| `.AddUser(out var user)` | One user with realistic fake values |
| `.AddUser(out var user, u => u.Role = "Admin")` | One user with the given value |
| `.AddUsers(out var users, 3)` | Three users per tenant in the anchor |
| `.AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")` | One user per customizer. A comma makes a new entity. |
| `.AddUsers(out var users, admin + named)` | One user with both customizers. `+` stacks customizers on one entity. |
| `.AddTenant(out var tenant).AddUser(out var user)` | A new tenant, then a user in that tenant |
| `.Under(seed.Tenant)` | Re-anchors to an entity that the session owns. Adds nothing. |

Rules for the anchor:

- Each `AddX` call moves the anchor to its outputs.
- A child `AddX` compiles only after its parent type. `AddUser` needs a `Tenant[]` anchor.
- If you need two users in one tenant, use `AddUsers` with two customizers. Two `AddUser` calls in a row do not compile.
- If you need to go back to the seed tenant after other steps, use `.Under(seed.Tenant)`.
- A child gets its parent key from the anchor. Never set a parent key in a customizer.

Rules for values:

- An input property that is null after the customizer gets a fake value from a `Derive`.
- An explicit value is kept, also an empty string. Use this to arrange invalid data for a validation test.
- Every entity goes through the real domain factory. Invalid input throws the domain exception at the `AddX` call.

## Engine features

These features are in `Core/`. Use them in `Blueprints.cs`, in `TestGraph.cs`, or in a test.

| Feature | Use |
|---|---|
| Sequence | `.Derive(i => i.Number, Sequence.Of(1, n => n + 1))` fills unset values. Explicit values in the same step are skipped. Each session restarts the sequence. |
| Sequence scope | `.PerSession()` (default), `.PerParent()`, or `.Per<Tenant>()` restarts the count for each parent or ancestor. |
| Frozen time | `ctx.Now`, `ctx.Ago(TimeSpan)`, and `ctx.FromNow(TimeSpan)`. Time is frozen when the session starts. Pass a `TimeProvider` to `TestGraph.Start` for a fixed time. For a date, use `ZonedDate.TodayAt(zone, ctx.Now)` with the zone of the entity that the date belongs to. Do not use `DateOnly.FromDateTime(ctx.Now.UtcDateTime)`. |
| Context-aware customizer | `.AddUsers(out var users, (u, ctx) => ...)` or `builder.AddOne(bp, out var x, (e, ctx) => ...)`. `ctx` has `Seed`, `Index`, `Now`, `Next(sequence)`, `Ancestor<T>()`, `All<T>()`, and `Last<T>()`. |
| Trait | `Blueprints.User.With(u => u.Role = "Admin")` makes a variant that sets the value on every output. |
| Transient | `bp.AsTransient()`, or `.Transient()` after an `AddX` call. The entities are built but `BuildAsync` does not save them. |
| Aggregate | Implement `IGraphAggregate.GetEntities()` on a wrapper type. `BuildAsync` saves the parts in order, not the wrapper. |
| Plain blueprint | `Blueprint.For` and `Blueprint.ChildOf` customize the entity directly. Use them only for types with settable properties. Domain entities have private setters, so use `FromInput` and `ChildFromInput` for them. |
| IDs | The entity makes its ID when the input `Id` is null. Set the input `Id` to give a fixed ID. In a customizer, `ctx.NewId(Order.IdPrefix)` makes a prefixed ID, because `TestGraph` sets `Ids(EntityId.New)`. |
| Options | `GraphOptions<TestSeed>` has `SeedEntities`, `Default<TBase, TProp>`, `BeforeSave`, `AfterSave`, `TimePrecision`, and `Ids`. The options freeze at `EntityGraph.Start`. |
| Store | `dbContext.AsGraphStore()` makes a `DbContextStore`. It adds all rows, saves once, and clears the change tracker. |

`TestGraph` selects the seed tenant and the seed user with `SeedEntities`, so they can be parents for `Under`. A `BeforeSave` hook in `TestGraph` takes them out of the save, because `TestFixture` saves the seed once per run.

## Errors

| Error | Cause |
|---|---|
| `does not contain a definition for 'AddUser'` | The anchor is not `Tenant[]`. Use `AddUsers` or `.Under(...)`. |
| `This graph session is faulted` | An earlier `AddX` call threw. Read the inner exception. |
| `This graph has already been built` | You called `AddX` or `BuildAsync` after `BuildAsync`. Use one session per scenario. |
| `is not owned by this graph session` | You passed an entity from a different session to `Under`. |
| `EntityGraph options are frozen` | You changed a `GraphOptions` after a session started with it. Configure the options before the first `Start`. |
| `GRAPH001` | Two generated methods have the same signature. Rename one blueprint with `[Graft("Name")]`. |
| `GRAPH002` | The plural name is the same as the singular name. Set `[Graft(Plural = "...")]`. |
| `GRAPH003` | `[GraphGraft]` is on a type that is not a top-level, non-generic, static class. |
| `GRAPH004` | A `[Graft]` name is not a valid C# identifier. |

## Add a new entity to the entity graph

Do these steps after the entity, its creation model, and its EF configuration exist. The `backend-feature` skill makes those.

1. In `Fakes/[EntityName]/[EntityName]Rules.cs`, add one static method per faked value. Each method takes a `Faker` and returns the value.

   ```csharp
   public static class OrderRules
   {
       public static string Number(Faker f) => f.Random.AlphaNumeric(10);
   }
   ```

   Do not use `f.Person` in a rule. A shared `Faker` caches one person, so every entity gets the same name. Use `f.Name.FirstName()` instead.

   A rule for a time zone picks it from a fixed list that has zones on both sides of UTC, for example `Pacific/Tongatapu` (UTC+13) and `America/Los_Angeles` (UTC-8). Then a test that takes today from the UTC date fails on most days, not only in the evening.

2. Make the shape fakers for the entity use the rules with method groups: `RuleFor(x => x.Number, OrderRules.Number)`.

3. In `Inputs.cs`, add an input class. Make every property settable and nullable. Do not add parent keys.

   ```csharp
   public sealed class OrderInput
   {
       public string? Id { get; set; }
       public string? Number { get; set; }
   }
   ```

   A test can set `Id` in a customizer. The ID must have the prefix of the entity.

4. In `Blueprints.cs`, add a blueprint. If the entity has no parent, use `Blueprint.FromInput`. If the entity belongs to a parent, use `Blueprint.ChildFromInput` and take the parent key from the parent. Add one `Derive` per input property, except `Id`. Pass `Id = input.Id` to the creation model. The input `Id` is null by default, so the entity makes the ID.

   ```csharp
   public static readonly ChildInputBlueprint<TestSeed, Tenant, OrderInput, Order> Order = Blueprint
       .ChildFromInput<TestSeed, Tenant, OrderInput, Order>(
           () => new(),
           (tenant, input, _) => Server.Domain.Orders.Order.Create(new OrderForCreation
           {
               Id = input.Id,
               TenantId = tenant.Id,
               Number = input.Number!,
           }))
       .Derive(i => i.Number, (_, _) => OrderRules.Number(Faker));
   ```

   The creation model needs a `string? Id` property. Its domain factory keeps the generated ID, or sets the given ID with `if (forCreation.Id is not null) entity.SetId(forCreation.Id);`. If the entity has child rows that copy its ID, the factory must create the children after it sets its ID. Make the shape faker set `Id` to null.

5. Build the solution. The generator makes `AddOrder` and `AddOrders` from the field name. A child method compiles only after its parent type, so `AddOrder` compiles only after a `Tenant[]` anchor.

6. If the generated name is wrong, put `[Graft("Name", Plural = "Names")]` on the field. If a field must not get methods, put `[NoGraft]` on it.

7. In `tests/FullstackTemplate.UnitTests/EntityGraphs/`, add a test that the child gets the parent key from the anchor.

8. Run `dotnet build FullstackTemplate.slnx`. Then run the unit tests and the integration tests.

Do not do these things:

- Do not set private entity state with reflection. Every entity must go through its domain factory.
- Do not save seed rows through the entity graph. `TestFixture` saves the seed once.
- Do not write `AddX` methods by hand. The generator makes them from `Blueprints.cs`.
