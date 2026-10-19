# Entity configurations

Each entity has one EF Core configuration class in `Databases/EntityConfigurations/`. `AppDbContext` loads all of them with `ApplyConfigurationsFromAssembly`. The example is `UserConfiguration.cs`.

## Rules

1. Make the class `sealed`, and implement `IEntityTypeConfiguration<T>`.
2. Do not configure an entity in `OnModelCreating`.
3. Give `Id` and each ID foreign key `HasMaxLength(EntityId.MaxLength)` and `UseCollation(EntityId.Collation)`.
4. Give each string property a `HasMaxLength`. Use 320 for an email.
5. Map each value object with `ComplexProperty`. Give its column a snake case name with `HasColumnName`. The naming convention makes the other names snake case.
6. Set `OnDelete` on each relationship. Use `Restrict` for a link to another aggregate. Use `Cascade` for a child that the entity owns.
7. Add an index for each property that queries filter or sort on.
8. After you change a configuration, add a migration. See [backend.md](../backend.md#migrations).

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

When you delete the parent, load the children and remove each child in the handler. See `Domain/Users/Features/DeleteUser.cs`.

## Unique indexes

The context does soft deletes, so a deleted row still holds its unique value. A unique index on a column of a soft-deleted entity also blocks the values of deleted rows. If a deleted row must free its value, add a filter to the index:

```csharp
builder.HasIndex(e => e.Email)
    .IsUnique()
    .HasFilter("is_deleted = false");
```
