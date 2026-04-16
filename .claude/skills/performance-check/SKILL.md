---
name: performance-check
description: "Analyze a path for performance issues — N+1 queries, missing indexes, caching opportunities, and async anti-patterns."
when_to_use: "When the user runs `/performance-check` or asks for performance analysis of specific Authra code."
version: 2.2.0
---

# Performance Check

Analyzes code for performance issues and recommends optimizations.

## Usage

```
/performance-check <path> [--focus <area>]
```

## Arguments

- `<path>`: File, folder, or feature to analyze (e.g., `src/Authra.Infrastructure/Services/`)
- `--focus`: Optional focus area (queries, indexes, caching, async)

## What This Skill Does

### Step 1: Gather Context

1. Read the target code files
2. Identify EF Core queries and data access patterns
3. Review entity relationships
4. Understand the feature's query patterns

### Step 2: EF Core Query Analysis

Check for query issues:

- [ ] No N+1 queries (missing `Include()`)
- [ ] Projections used for read-only data
- [ ] `AsNoTracking()` for read-only queries
- [ ] Compiled queries for hot paths
- [ ] Proper pagination
- [ ] No `ToList()` before `Where()`

**N+1 Query Pattern:**

```csharp
// BAD: N+1 queries - one query per tenant
var tenants = await context.Tenants.ToListAsync();
foreach (var tenant in tenants)
{
    var members = tenant.Members;  // Lazy load!
}

// GOOD: Eager loading
var tenants = await context.Tenants
    .Include(t => t.Members)
    .ToListAsync();
```

**Missing Projection:**

```csharp
// BAD: Loads entire entity
var users = await context.Users
    .Where(u => u.TenantId == tenantId)
    .ToListAsync();
return users.Select(u => new UserDto(u.Id, u.Email));

// GOOD: Database projection
var users = await context.Users
    .Where(u => u.TenantId == tenantId)
    .Select(u => new UserDto(u.Id, u.Email))
    .ToListAsync();
```

**Tracking Overhead:**

```csharp
// BAD: Tracks entities unnecessarily
var tenants = await context.Tenants.ToListAsync();

// GOOD: No tracking for reads
var tenants = await context.Tenants
    .AsNoTracking()
    .ToListAsync();
```

### Step 3: Index Analysis

Check for missing indexes:

- [ ] Indexes on filter columns
- [ ] Indexes on foreign keys
- [ ] Composite indexes for multi-column queries
- [ ] Unique indexes where applicable
- [ ] GIN indexes for JSONB queries

**Identify Missing Indexes:**

```csharp
// Query pattern
var member = await context.TenantMembers
    .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId);

// Requires index
builder.HasIndex(m => new { m.UserId, m.TenantId });
```

**Recommended Indexes for Authra:**

```sql
-- Authentication
CREATE INDEX ix_user_identifiers_type_value ON user_identifiers (type, value_normalized);
CREATE UNIQUE INDEX ix_refresh_tokens_hash ON refresh_tokens (token_hash);
CREATE INDEX ix_token_blacklist_jti ON token_blacklist (jti, expires_at);

-- Multi-tenant
CREATE INDEX ix_tenant_members_user_tenant ON tenant_members (user_id, tenant_id);
CREATE INDEX ix_org_members_org ON organization_members (organization_id);
CREATE INDEX ix_tenant_member_roles_member ON tenant_member_roles (tenant_member_id);

-- JSONB
CREATE INDEX ix_external_auth_provider_data ON external_auth USING gin (provider_data);
```

### Step 4: Caching Analysis

Identify caching opportunities:

- [ ] Static data cached (permissions, roles)
- [ ] Frequently accessed data cached
- [ ] Cache invalidation strategy
- [ ] Appropriate cache durations

**Cacheable Data:**

```csharp
// Permissions rarely change - cache for 1 hour
public class PermissionCache(IMemoryCache cache, AppDbContext context)
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync()
    {
        return await cache.GetOrCreateAsync("permissions", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await context.Permissions.AsNoTracking().ToListAsync();
        });
    }
}

// JWT signing keys - cache for 5 minutes
public async Task<SigningKey> GetCurrentKeyAsync()
{
    return await cache.GetOrCreateAsync("signing-key", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return await context.SigningKeys
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .FirstAsync();
    });
}
```

### Step 5: Async Pattern Analysis

Check async/await usage:

- [ ] No `.Result` or `.Wait()` blocking calls
- [ ] `ConfigureAwait(false)` in library code
- [ ] Proper parallel execution where applicable
- [ ] No unnecessary async overhead

**Blocking Calls:**

```csharp
// BAD: Blocking on async
var user = GetUserAsync(id).Result;

// GOOD: Async all the way
var user = await GetUserAsync(id);
```

**Parallel Execution:**

```csharp
// BAD: Sequential when parallel possible
var user = await GetUserAsync(id);
var tenant = await GetTenantAsync(tenantId);

// GOOD: Parallel independent operations
var userTask = GetUserAsync(id);
var tenantTask = GetTenantAsync(tenantId);
await Task.WhenAll(userTask, tenantTask);
var user = await userTask;
var tenant = await tenantTask;
```

### Step 6: Memory Analysis

Check for memory issues:

- [ ] No unnecessary allocations in hot paths
- [ ] Proper use of `Span<T>` / `Memory<T>`
- [ ] ArrayPool for temporary buffers
- [ ] String concatenation optimized

**String Building:**

```csharp
// BAD: Creates many intermediate strings
var result = "";
foreach (var item in items)
    result += item.ToString();

// GOOD: StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
    sb.Append(item);
var result = sb.ToString();
```

### Step 7: Generate Report

Output a structured performance report:

```markdown
# Performance Analysis Report

**Target**: {path}
**Date**: {date}

## Summary
{Overall performance assessment}

## Critical Issues
{Immediate performance problems}

## Optimization Opportunities
{Recommended improvements}

## Index Recommendations
{New indexes to create}

## Caching Recommendations
{Data to cache}

## Metrics
{Expected improvements}
```

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `database-conventions` | Entity relationships |
| `api-conventions` | Pagination patterns |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `performance-analyzer` | Primary analysis |
| `persistence-specialist` | Query optimization |

## Context7 Usage

Query these libraries:
- `/efcore/efcore` - EF Core query optimization
- `/npgsql/npgsql` - PostgreSQL indexing

## Example

```bash
/performance-check src/Authra.Infrastructure/Services/TenantService.cs
```

**Outputs:**

```markdown
# Performance Analysis Report

**Target**: TenantService.cs
**Date**: 2026-01-25

## Summary
Found 1 N+1 query pattern and 2 missing indexes.
Estimated 60% improvement in GetAllAsync with fixes.

## Critical Issues

### 1. N+1 Query in GetAllAsync
**Line**: 45
**Issue**: Members loaded lazily in loop

```csharp
// Current (N+1)
var tenants = await context.Tenants.ToListAsync();
foreach (var t in tenants)
{
    dto.MemberCount = t.Members.Count;  // Lazy load!
}

// Fixed
var tenants = await context.Tenants
    .Include(t => t.Members)
    .AsNoTracking()
    .ToListAsync();
```

## Optimization Opportunities

### 1. Add Projection
**Line**: 67
**Issue**: Loading full entity for DTO mapping

```csharp
// Current
var tenant = await context.Tenants.FindAsync(id);
return MapToDto(tenant);

// Optimized
var tenant = await context.Tenants
    .Where(t => t.Id == id)
    .Select(t => new TenantDto(
        IdEncoder.Encode("Tenant", t.Id),
        t.Name,
        t.Description,
        t.CreatedAt))
    .FirstOrDefaultAsync();
```

### 2. Add AsNoTracking
**Line**: 78
**Issue**: GetBySlugAsync doesn't need tracking

## Index Recommendations

```sql
-- Supports GetBySlugAsync
CREATE UNIQUE INDEX ix_tenants_slug ON tenants (slug);

-- Supports member count queries
CREATE INDEX ix_tenant_members_tenant ON tenant_members (tenant_id);
```

## Caching Recommendations

None identified - tenant data changes frequently.

## Expected Improvements

| Operation | Before | After | Improvement |
|-----------|--------|-------|-------------|
| GetAllAsync (10 tenants) | ~110ms | ~15ms | 86% |
| GetByIdAsync | ~8ms | ~3ms | 62% |
```

## Checklist

Performance check should verify:

- [ ] No N+1 query patterns
- [ ] Projections used appropriately
- [ ] AsNoTracking for read-only
- [ ] Required indexes exist
- [ ] Caching opportunities identified
- [ ] No blocking async calls
- [ ] Pagination implemented correctly
- [ ] No memory issues in hot paths
