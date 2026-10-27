# Entity configurations

Each entity has one EF Core configuration class in `Databases/EntityConfigurations/`. `AppDbContext` loads all of them with `ApplyConfigurationsFromAssembly`. The example is `UserConfiguration.cs`.

## Rules

1. Make the class `sealed`, and implement `IEntityTypeConfiguration<T>`.
2. Do not configure an entity in `OnModelCreating`.
3. Give `Id` and each ID foreign key `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`.
4. Give each string property a `HasMaxLength`. For a value that a request sets, use the max length constant of the entity or the value object, for example `User.FirstNameMaxLength` or `EmailAddress.MaxLength`. The entity refuses a longer value. See [domain-entities.md](domain-entities.md#rules).
5. Map each value object with `ComplexProperty`. Give its column a snake case name with `HasColumnName`. The naming convention makes the other names snake case. Do not use `OwnsOne`. The audit log reads only entities and their complex properties, and `AuditModelTests` fails on an owned type.
6. Set `OnDelete` on each relationship. Use `ClientNoAction` for a link to another aggregate. The context turns a delete into a soft delete, so the delete of an entity must not change another aggregate. `Restrict`, `NoAction`, `SetNull`, and `ClientSetNull` set the foreign key of a loaded dependent to null. The save then fails, or it writes the null key, but the principal row stays. `DeleteBehaviorTests` fails on each of them. Use `Cascade` for a child that the entity owns. `Cascade` also tells the context that a change to the child updates the owner row, so that the version of the owner guards the child. It also makes the owner the aggregate root of the child in the audit log. Give a child one `Cascade` foreign key to an entity. See [backend.md](../backend.md#concurrency).
7. Add an index for each property that queries filter or sort on.
8. After you change a configuration, add a migration. See [backend.md](../backend.md#migrations).
9. Do not configure `Version`. `MapVersionToXmin` maps it to the `xmin` column of each entity.

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
