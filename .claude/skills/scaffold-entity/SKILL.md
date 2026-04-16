---
name: scaffold-entity
description: "Scaffold a complete entity with Domain model, EF Core configuration, and migration following Authra conventions."
when_to_use: "When the user runs `/scaffold-entity` or asks to create a new entity from the data model."
version: 2.2.0
---

# Scaffold Entity

Creates a new entity across Domain and Infrastructure layers with proper Authra conventions.

## Usage

```
/scaffold-entity <EntityName>
```

## Arguments

- `<EntityName>`: PascalCase entity name (e.g., Organization, TenantMember)

## Prerequisites

Before scaffolding, ensure the entity is defined in CLAUDE-DATA-MODEL.md with:
- Column definitions
- Relationships
- Constraints
- RLS requirements

## What This Skill Does

### Step 1: Analyze Data Model

1. Read CLAUDE-DATA-MODEL.md for entity specification
2. Identify columns, types, relationships
3. Determine if entity is tenant-scoped (requires TenantId for RLS)
4. Identify foreign keys and navigation properties

### Step 2: Create Domain Entity

Create `src/Authra.Domain/Entities/{EntityName}.cs`:

```csharp
namespace Authra.Domain.Entities;

public class {EntityName}
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    // ... other properties from data model
}
```

**Patterns to apply:**
- Inherit from `Entity` or `TenantEntity` base class if defined
- Use `init` for immutable properties, `set` for mutable
- Use nullable reference types correctly
- Add navigation properties for relationships

### Step 3: Create EF Core Configuration

Create `src/Authra.Infrastructure/Persistence/Configurations/{EntityName}Configuration.cs`:

```csharp
namespace Authra.Infrastructure.Persistence.Configurations;

public class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.ToTable("{table_name}");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("uuidv7()");

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()");

        // Unique constraints
        // Foreign keys
        // Indexes
        // RLS column if tenant-scoped
    }
}
```

**Patterns to apply:**
- Table name: lowercase snake_case
- UUID v7 default for Id
- `now()` default for CreatedAt
- Enums stored as strings
- JSONB for flexible data columns
- Proper cascade delete rules

### Step 4: Update AppDbContext

Add DbSet to `src/Authra.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
public DbSet<{EntityName}> {EntityName}s => Set<{EntityName}>();
```

### Step 5: Generate Migration

Run migration command:

```bash
dotnet ef migrations add Add{EntityName}Entity \
    --project src/Authra.Infrastructure \
    --startup-project src/Authra.Api
```

### Step 6: Review Output

Validate the generated code against:
- `database-conventions` convention skill
- `csharp-conventions` convention skill
- `architecture-conventions` convention skill

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `database-conventions` | Entity patterns, UUID v7, RLS, relationships |
| `csharp-conventions` | C# 14 coding standards, naming |
| `architecture-conventions` | Domain layer placement |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `persistence-specialist` | EF Core configuration, migration generation |
| `code-reviewer` | Validate output against conventions |

## Context7 Usage

Query these libraries for up-to-date patterns:
- `/efcore/efcore` - EF Core 10 configuration patterns
- `/npgsql/npgsql` - PostgreSQL-specific features

## Example

```bash
/scaffold-entity Organization
```

**Generates:**

`src/Authra.Domain/Entities/Organization.cs`:
```csharp
namespace Authra.Domain.Entities;

public class Organization
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; init; } = null!;
    public ICollection<OrganizationMember> Members { get; init; } = [];
}
```

`src/Authra.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs`:
```csharp
namespace Authra.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("uuidv7()");

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        // Tenant relationship (RLS)
        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.Organizations)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}
```

## Checklist

After running this skill, verify:

- [ ] Entity class created in Domain layer
- [ ] EF Core configuration created
- [ ] DbSet added to AppDbContext
- [ ] Migration generated
- [ ] UUID v7 default configured
- [ ] CreatedAt default configured
- [ ] RLS column (TenantId) present if tenant-scoped
- [ ] Foreign keys configured correctly
- [ ] Indexes added for query patterns
- [ ] Unique constraints defined
