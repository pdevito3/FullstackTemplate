# Entity configurations

Each entity has one EF Core configuration class in `Databases/EntityConfigurations/`. `AppDbContext` loads all of them with `ApplyConfigurationsFromAssembly`. The example is `UserConfiguration.cs`.

## Rules

1. Make the class `sealed`, and implement `IEntityTypeConfiguration<T>`.
2. Do not configure an entity in `OnModelCreating`.
3. Give `Id` and each ID foreign key `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`.
4. Give each string property a `HasMaxLength`. Use 320 for an email.
5. Map each value object with `ComplexProperty`. Give its column a snake case name with `HasColumnName`. The naming convention makes the other names snake case.
6. Set `OnDelete` on each relationship. Use `Restrict` for a link to another aggregate. Use `Cascade` for a child that the entity owns. `Cascade` also tells the context that a change to the child updates the owner row, so that the version of the owner guards the child. See [backend.md](../backend.md#concurrency).
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
            .HasMaxLength(200);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Identifier)
            .IsUnique();
    }
}
```

## Value objects

```csharp
builder.ComplexProperty(e => e.Email, email =>
{
    email.Property(e => e.Value)
        .HasColumnName("email")
        .HasMaxLength(320)
        .IsRequired();
});
```

## Private collections

An entity keeps a child collection in a private field. Tell EF Core to use the field:

```csharp
builder.HasMany(e => e.UserPermissions)
    .WithOne()
    .HasForeignKey(up => up.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Navigation(e => e.UserPermissions)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

When you delete the parent, load the children and remove each child in the handler. See `Domain/Users/Features/DeleteUser/DeleteUser.cs`.

## Unique indexes

The context does soft deletes, so a deleted row still holds its unique value. A unique index on a column of a soft-deleted entity also blocks the values of deleted rows. If a deleted row must free its value, add a filter to the index:

```csharp
builder.HasIndex(e => e.Email)
    .IsUnique()
    .HasFilter("is_deleted = false");
```

Give each unique index its request field in `ConstraintConflictException.ConstraintFields`, so that a duplicate gives a 409 on that field.

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
