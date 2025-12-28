# Entity Configurations

Guidelines for creating EF Core entity configurations using `IEntityTypeConfiguration<T>`.

## Directory Structure

Entity configurations are located in the Databases folder:

```
Databases/
└── EntityConfigurations/
    ├── CustomerConfiguration.cs
    ├── OrderConfiguration.cs
    └── [EntityName]Configuration.cs
```

## Configuration Structure

### Basic Configuration Template

```csharp
namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using FullstackTemplate.Server.Domain.[EntityName]s;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class [EntityName]Configuration : IEntityTypeConfiguration<[EntityName]>
{
    public void Configure(EntityTypeBuilder<[EntityName]> builder)
    {
        // Table name (optional - EF uses DbSet name by default)
        // builder.ToTable("[entity_name]s");

        // Primary key (inherited from BaseEntity, but can be explicit)
        builder.HasKey(e => e.Id);

        // Property configurations
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        // Indexes
        builder.HasIndex(e => e.Name);
    }
}
```

## Common Configuration Patterns

### Value Object Configurations

For owned value objects:

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Owned value object - stored as columns in same table
        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(320);
        });

        builder.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("address_state").HasMaxLength(50);
            address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
        });
    }
}
```

### SmartEnum/Status Value Objects

For value objects backed by SmartEnum:

```csharp
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Status stored as string
        builder.OwnsOne(e => e.Status, status =>
        {
            status.Property(s => s.Value)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();
        });
    }
}
```

### Relationship Configurations

```csharp
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // One-to-many relationship
        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-many (with join entity)
        builder.HasMany(e => e.Products)
            .WithMany(p => p.Orders)
            .UsingEntity<OrderProduct>();
    }
}
```

### Encapsulated Collection Configurations

For private backing fields:

```csharp
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Access private backing field for collection
        builder.HasMany(e => e.LineItems)
            .WithOne()
            .HasForeignKey(li => li.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure navigation to use backing field
        builder.Navigation(e => e.LineItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

### Index Configurations

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Simple index
        builder.HasIndex(e => e.Name);

        // Unique index
        builder.HasIndex(e => e.Email)
            .IsUnique();

        // Composite index
        builder.HasIndex(e => new { e.LastName, e.FirstName });

        // Filtered index (for soft delete scenarios)
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
```

### Computed Columns

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Computed column (PostgreSQL syntax)
        builder.Property(e => e.FullName)
            .HasComputedColumnSql("first_name || ' ' || last_name", stored: true);
    }
}
```

## Registering Configurations

### In AppDbContext

Apply all configurations from the assembly:

```csharp
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Apply soft delete filter
        modelBuilder.FilterSoftDeletedRecords();
    }
}
```

### Manual Registration (Alternative)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    modelBuilder.ApplyConfiguration(new OrderConfiguration());

    modelBuilder.FilterSoftDeletedRecords();
}
```

## Key Principles

### 1. One Configuration Per Entity

Each entity gets its own configuration class:

```csharp
// Good - separate files
CustomerConfiguration.cs
OrderConfiguration.cs
ProductConfiguration.cs

// Bad - all in OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>(e => { /* lots of config */ });
    modelBuilder.Entity<Order>(e => { /* lots of config */ });
    // ... hundreds of lines
}
```

### 2. Sealed Configuration Classes

Mark configurations as sealed:

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
```

### 3. Configure Value Objects with OwnsOne

Value objects should be owned types:

```csharp
builder.OwnsOne(e => e.Email, email =>
{
    email.Property(e => e.Value).HasColumnName("email");
});
```

### 4. Use Explicit Column Names for Clarity

When column names differ from property names:

```csharp
builder.Property(e => e.FirstName)
    .HasColumnName("first_name");  // snake_case for PostgreSQL
```

### 5. Set Appropriate MaxLength

Always set max length for string properties:

```csharp
builder.Property(e => e.Name)
    .HasMaxLength(200);

builder.Property(e => e.Email)
    .HasMaxLength(320);  // Max email length per RFC

builder.Property(e => e.Description)
    .HasMaxLength(4000);
```

### 6. Configure Delete Behavior

Be explicit about cascade behavior:

```csharp
// Prevent accidental cascade deletes
builder.HasOne(e => e.Customer)
    .WithMany(c => c.Orders)
    .OnDelete(DeleteBehavior.Restrict);

// Allow cascade for owned entities
builder.HasMany(e => e.LineItems)
    .WithOne()
    .OnDelete(DeleteBehavior.Cascade);
```

## Configuration Checklist

When creating a new entity configuration:

- [ ] Create sealed class implementing `IEntityTypeConfiguration<T>`
- [ ] Configure primary key (if not inherited)
- [ ] Set max lengths for all string properties
- [ ] Configure value objects with `OwnsOne`
- [ ] Set up relationships with explicit foreign keys
- [ ] Add indexes for frequently queried properties
- [ ] Configure delete behaviors appropriately
- [ ] Test with `dotnet ef migrations add` to verify

## Anti-Patterns to Avoid

### Don't Configure in OnModelCreating

```csharp
// Bad - inline configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>(e =>
    {
        e.HasKey(c => c.Id);
        e.Property(c => c.Name).HasMaxLength(200);
        // ... many more lines
    });
}

// Good - use configuration class
modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
```

### Don't Forget Value Object Configuration

```csharp
// Bad - value object not configured (will fail)
public EmailAddress Email { get; private set; }

// Good - explicitly configured
builder.OwnsOne(e => e.Email, email =>
{
    email.Property(e => e.Value).HasColumnName("email");
});
```

### Don't Use Magic Strings for Column Names

```csharp
// Bad - magic strings
builder.Property(e => e.Email).HasColumnName("customer_email");

// Good - use nameof when possible, or constants
private const string EmailColumn = "email";
builder.Property(e => e.Email).HasColumnName(EmailColumn);
```
