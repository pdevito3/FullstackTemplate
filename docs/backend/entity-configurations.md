# Entity configurations

Each entity has one EF Core configuration class in `Databases/EntityConfigurations/`. `AppDbContext` loads all of them with `ApplyConfigurationsFromAssembly`. The example is `UserConfiguration.cs`.

## Rules

1. Make the class `sealed`, and implement `IEntityTypeConfiguration<T>`.
2. Do not configure an entity in `OnModelCreating`.
3. Give `Id` and each ID foreign key `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`.
4. Give each string property a `HasMaxLength`. For a value that a request sets, use the max length constant of the entity or the value object, for example `User.FirstNameMaxLength` or `EmailAddress.MaxLength`. The entity refuses a longer value. See [domain-entities.md](domain-entities.md#rules).
5. Map each value object with `ComplexProperty`. Give its column a snake case name with `HasColumnName`. The naming convention makes the other names snake case. Do not use `OwnsOne`. The audit log reads only entities and their complex properties, and `AuditModelTests` fails on an owned type.
6. Set `OnDelete` on each relationship. Use `ClientNoAction` for a link to another aggregate. The context turns a delete into a soft delete, so the delete of an entity must not change another aggregate. `Restrict`, `NoAction`, `SetNull`, and `ClientSetNull` set the foreign key of a loaded dependent to null. The save then fails, or it writes the null key, but the principal row stays. `DeleteBehaviorTests` fails on each of them. Use `Cascade` for a child that the entity owns. `Cascade` also tells the context that a change to the child updates the owner row, so that the version of the owner guards the child. It also makes the owner the aggregate root of the child in the audit log. Give a child one `Cascade` foreign key to an entity. A nullable `Cascade` foreign key makes an owner that is optional, and the child is a root when the key is null. See [concurrency.md](concurrency.md#versions) and [Trees](#trees).
7. Add an index for each property that queries filter or sort on.
8. After you change a configuration, add a migration. See [backend.md](../backend.md#migrations).
9. Do not configure `Version`. `MapVersionToXmin` maps it to the `xmin` column of each entity.
10. Map each `BaseEntity` to a table, with `Id` as its key. The model build fails for a `BaseEntity` whose key is not `Id` alone, and for a `BaseEntity` that maps to a view, a SQL query, or a function. To read a view, see [Read models](#read-models).

## Example

```csharp
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(User.FirstNameMaxLength);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(e => e.Identifier)
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
```

## Value objects

```csharp
builder.ComplexProperty(e => e.Email, email =>
{
    email.Property(e => e.Value)
        .HasColumnName("email")
        .HasMaxLength(EmailAddress.MaxLength)
        .IsRequired();
});
```

EF Core 10 cannot put an index on a column of a complex property. To index one, see [Index a column of a value object](#index-a-column-of-a-value-object).

## Private collections

An entity keeps a child collection in a private field. Tell EF Core to use the field:

```csharp
builder.HasMany(e => e.UserPermissions)
    .WithOne(up => up.User)
    .HasForeignKey(up => up.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(e => e.UserPermissions)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

Give the child a navigation to its parent, and get its tenant through the parent. See [multi-tenancy.md](../multi-tenancy.md#make-a-child-entity-multi-tenant).

```csharp
builder.ApplyTenanting(up => up.User.TenantId);
```

When you delete the parent, load the children and remove each child in the handler. See `Domain/Users/Features/DeleteUser/DeleteUser.cs`.

## Trees

An entity with a foreign key to a parent of its own type is a tree, for example a folder tree. Pick one of two shapes:

- **Each node is an aggregate root.** Link the parent with `ClientNoAction`. Each node has its own version and its own audit history, and each node implements `ITenantable`. Use this shape when a change of a node does not read the other nodes.
- **The tree is one aggregate.** Link the parent with a nullable `Cascade` foreign key. A node with no parent is the aggregate root. Each other node is a part of the aggregate of its top node, and its audit entries name the top node as the root. Use this shape only when a rule reads the whole tree, because each change of a node also updates the row of each ancestor.

For a tree that is one aggregate, obey these rules:

1. Implement `ITenantable` on the node, because a node with no parent is a root. Give each new node the tenant of its parent, and move a node only under a node of the same tenant. No check finds a node with a tenant that is not the tenant of its top node. `AuditModelTests` fails when the node has no tenant.
2. Load each ancestor of a node before you change the node. The save throws when an ancestor is not tracked.
3. To move a node, load its former parent and the ancestors of the former parent too. The save updates the rows of the former ancestors and of the new ancestors, so the version of each top node guards the move. The audit entry of the move names the new root and holds the old and the new parent key.
4. Do not bulk insert the nodes. `BulkInsertAsync` refuses a type with an owner key, also a key that is optional.
5. Make the subject of an audit action from the top node, not from the node that the action is about. The save throws when the subject names a tracked node that has a parent. It does not see a node that you read with `AsNoTracking`.

`EntityTreeTests` is the example.

## Read models

A `BaseEntity` is a row of a table. Its ID, its `xmin` version, the soft delete filter, and the audit log need the table and the key. A query of a view selects `xmin`, and a view has no `xmin` column, so Postgres fails the query. Thus a read model of a view is a class that is not a `BaseEntity`:

1. Give the class `[NotAudited(reason)]`, because the save does not write it.
2. Implement `ITenantable` on the class, so that the tenant filter applies to its rows.
3. In its configuration, call `HasNoKey()` and `ToView("view_name")`.
4. Create the view with `migrationBuilder.Sql` in a migration. EF does not create a view. Leave out the deleted rows in the view (`WHERE NOT is_deleted`), because the soft delete filter does not apply to a class that is not a `BaseEntity`.

```csharp
[NotAudited("A read model of a view. The save never writes it.")]
public sealed class BoxCount : ITenantable
{
    public string TenantId { get; private set; } = null!;
    public int Count { get; private set; }
}

builder.HasNoKey().ToView("box_counts");
```

`ReadModelViewTests` is the example.

## Unique indexes

The context does soft deletes, so a deleted row still holds its unique value. A unique index on a column of a soft-deleted entity also blocks the values of deleted rows. If a deleted row must free its value, add a filter to the index. `UserConfiguration` does this for `Identifier`, so that a deleted user can sign up again:

```csharp
builder.HasIndex(e => e.Identifier)
    .IsUnique()
    .HasFilter("is_deleted = false");
```

The duplicate check of the handler must count the same rows as the index. For a filtered index, keep the soft delete filter in the check. See [backend.md](../backend.md#error-responses).

Give each unique index its request field in `ConstraintConflictException.ConstraintFields`, so that a duplicate gives a 409 on that field. `ConstraintFieldsTests` reads the indexes of the database, so it fails on a unique index with no entry. This includes an index that only a migration adds.

### Index a column of a value object

EF Core 10 cannot put an index on a property of a complex type. `HasIndex(e => e.Email)`, `HasIndex(e => e.Email.Value)`, and `HasIndex("Email.Value")` each fail when EF builds the model, so the app cannot start. [EF Core 11](https://learn.microsoft.com/ef/core/modeling/indexes#indexes-on-complex-type-properties) adds this support. Until then, add the index in a migration. For example, to make the email of each live user unique:

1. Add an empty migration with `dotnet ef migrations add` (see [backend.md](../backend.md#migrations)). The configuration does not change, so EF writes an empty `Up` and `Down`.
2. Create the index in `Up`, and drop it in `Down`. Use the column name that the `ComplexProperty` mapping gives with `HasColumnName`.

   ```csharp
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.CreateIndex(
           name: "ix_users_email",
           table: "users",
           column: "email",
           unique: true,
           filter: "is_deleted = false");
   }

   protected override void Down(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.DropIndex(
           name: "ix_users_email",
           table: "users");
   }
   ```

3. Add the index to `ConstraintConflictException.ConstraintFields`, for example `["ix_users_email"] = "email"`.

The model and the model snapshot do not have the index. Thus a later migration does not drop it, and `dotnet ef migrations has-pending-model-changes` finds no change. A save that the index stops gives a 409 on the field, the same as for an index in the model.

## Exclusion constraints

An exclusion constraint stops two rows that overlap, for example two appointments of one provider at the same time. EF Core cannot configure one, so add it with SQL in the migration. A constraint with `=` on a scalar column in a `gist` index needs the `btree_gist` extension:

```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
migrationBuilder.Sql("""
    ALTER TABLE appointments ADD CONSTRAINT ex_appointments_provider_time
    EXCLUDE USING gist (provider_id WITH =, tstzrange(starts_at, ends_at) WITH &&)
    WHERE (is_deleted = false);
    """);
```

Then give the constraint its request field in `ConstraintConflictException.ConstraintFields`, for example `["ex_appointments_provider_time"] = "startsAt"`. A save that the constraint stops (Postgres `23P01`) then gives a 409 on that field, the same as a unique index.
