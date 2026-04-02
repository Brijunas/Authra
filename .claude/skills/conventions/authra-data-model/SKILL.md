# Authra Data Model Conventions

This skill defines entity and EF Core patterns for the Authra multi-tenant identity system.

## Context7 Usage

When implementing data model patterns, use Context7 MCP tools to query:
- `/efcore/efcore` - EF Core 10 configuration, migrations, interceptors
- `/npgsql/npgsql` - PostgreSQL provider specifics, RLS, UUID v7

## Entity Base Patterns

### Global Entities (no tenant scope)

```csharp
public abstract class Entity
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

**Global entities:** User, UserIdentifier, PasswordAuth, ExternalAuth, PasswordResetToken, SigningKey

### Tenant-Scoped Entities (RLS)

```csharp
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; init; }
}
```

**Tenant-scoped entities:** Tenant, TenantMember, Invite, Organization, OrganizationMember, Role, RolePermission, RoleOrganization, TenantMemberRole, OwnershipTransfer, RefreshToken, TokenBlacklist

## UUID v7 Configuration

Always use PostgreSQL native `uuidv7()` function:

```csharp
modelBuilder.Entity<User>()
    .Property(u => u.Id)
    .HasDefaultValueSql("uuidv7()");

modelBuilder.Entity<User>()
    .Property(u => u.CreatedAt)
    .HasDefaultValueSql("now()");
```

For application-side generation, use `Medo.Uuid7`:

```csharp
using Medo;
var id = Uuid7.NewUuid7().ToGuid();
```

## Table Naming

Use lowercase snake_case via `ToTable()`:

```csharp
modelBuilder.Entity<User>().ToTable("users");
modelBuilder.Entity<UserIdentifier>().ToTable("user_identifiers");
modelBuilder.Entity<TenantMember>().ToTable("tenant_members");
```

## Unique Constraints

### UserIdentifier

```csharp
modelBuilder.Entity<UserIdentifier>()
    .HasIndex(ui => new { ui.Type, ui.ValueNormalized })
    .IsUnique();
```

### ExternalAuth

```csharp
modelBuilder.Entity<ExternalAuth>()
    .HasIndex(ea => new { ea.Provider, ea.SubjectId })
    .IsUnique();
```

### RefreshToken

```csharp
modelBuilder.Entity<RefreshToken>()
    .HasIndex(rt => rt.TokenHash)
    .IsUnique();
```

## Enum Storage

Store enums as strings for database readability:

```csharp
modelBuilder.Entity<UserIdentifier>()
    .Property(ui => ui.Type)
    .HasConversion<string>();
```

**Project enums:**
- `IdentifierType` (Email, Username, Phone)
- `InviteStatus` (Pending, Accepted, Expired, Revoked)

## JSON Columns

For flexible provider data:

```csharp
modelBuilder.Entity<ExternalAuth>()
    .Property(ea => ea.ProviderData)
    .HasColumnType("jsonb");
```

## Foreign Key Cascades

- **User deletion:** Cascade to UserIdentifier, PasswordAuth, ExternalAuth, PasswordResetToken
- **Tenant deletion:** Cascade to all tenant-scoped entities
- **TenantMember removal:** Cascade to TenantMemberRole
- **Role deletion:** Cascade to RolePermission, RoleOrganization, TenantMemberRole

## Row-Level Security

### RLS Policy Pattern

```sql
ALTER TABLE {table_name} ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON {table_name}
    USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

### EF Core Interceptor

Set tenant context before queries:

```csharp
public class TenantInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public override async ValueTask<InterceptionResult> ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        InterceptionResult result,
        CancellationToken ct)
    {
        if (_tenantContext.TenantId.HasValue)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SET app.current_tenant = @p";
            var param = cmd.CreateParameter();
            param.ParameterName = "@p";
            param.Value = _tenantContext.TenantId.Value;
            cmd.Parameters.Add(param);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return result;
    }
}
```

## Migration Patterns

### Creating Migrations

```bash
dotnet ef migrations add {Name} \
    --project src/Authra.Infrastructure \
    --startup-project src/Authra.Api
```

### Migration Grouping

1. Identity tables (User, UserIdentifier, PasswordAuth, ExternalAuth)
2. Tenant tables (Tenant, TenantMember, Invite)
3. Organization tables (Organization, OrganizationMember)
4. RBAC tables (Permission, Role, RolePermission, RoleOrganization, TenantMemberRole)
5. Session tables (SigningKey, RefreshToken, TokenBlacklist)
6. RLS policies for tenant-scoped tables

## Timestamp Handling

Entities with modification tracking:

```csharp
public DateTime? UpdatedAt { get; set; }
```

**Applies to:** User, Tenant, Organization, Role, RefreshToken

Handle in `SaveChanges`:

```csharp
public override int SaveChanges()
{
    foreach (var entry in ChangeTracker.Entries<Entity>()
        .Where(e => e.State == EntityState.Modified))
    {
        if (entry.Entity is IHasUpdatedAt entity)
            entity.UpdatedAt = DateTime.UtcNow;
    }
    return base.SaveChanges();
}
```

## Soft Delete

Use `DeletedAt` for soft deletes:

```csharp
public DateTime? DeletedAt { get; set; }
```

**Applies to:** TenantMember, Invite (expired), RefreshToken (revoked)

Query filter:

```csharp
modelBuilder.Entity<TenantMember>()
    .HasQueryFilter(m => m.DeletedAt == null);
```
