---
name: review
description: "Comprehensive code review with architecture and convention validation against Authra standards."
when_to_use: "When the user runs `/review` or asks for a general code review of a path or feature."
version: 2.2.0
---

# Code Review

Performs a comprehensive code review checking quality, conventions, and architecture.

## Usage

```
/review <path> [--strict]
```

## Arguments

- `<path>`: File or folder to review (e.g., `src/Authra.Application/Tenants/`)
- `--strict`: Enable stricter checks (recommended before merging)

## What This Skill Does

### Step 1: Gather Context

1. Read the target code files
2. Identify the layer (Domain, Application, Infrastructure, Api)
3. Load relevant convention skills
4. Understand the feature being implemented

### Step 2: Architecture Review

Check Clean Architecture compliance:

- [ ] Domain layer has no external dependencies
- [ ] Application layer only references Domain
- [ ] Infrastructure implements Application interfaces
- [ ] No DbContext in Application layer
- [ ] No HTTP concerns outside Api layer
- [ ] Feature folders organized correctly

**Violations to detect:**

```csharp
// BAD: Application layer using DbContext
namespace Authra.Application.Tenants;

public class TenantService(AppDbContext context)  // WRONG!
{
}

// GOOD: Application defines interface only
namespace Authra.Application.Tenants;

public interface ITenantService { }
```

### Step 3: Code Style Review

Check C# 14 / .NET 10 conventions:

- [ ] One non-private type per file (class, record, struct, enum, interface)
- [ ] File-scoped namespaces
- [ ] Primary constructors for DI
- [ ] Records for DTOs
- [ ] Collection expressions
- [ ] Nullable reference types
- [ ] Consistent naming (PascalCase, camelCase)
- [ ] Async suffix on async methods
- [ ] CancellationToken passed through

**Patterns to check:**

```csharp
// BAD: Traditional constructor
public class TenantService
{
    private readonly AppDbContext _context;
    public TenantService(AppDbContext context)
    {
        _context = context;
    }
}

// GOOD: Primary constructor
public class TenantService(AppDbContext context)
{
}
```

### Step 4: Error Handling Review

Check exception patterns:

- [ ] Domain exceptions for business errors
- [ ] No generic catch blocks
- [ ] No swallowed exceptions
- [ ] Proper exception messages
- [ ] Validation at boundaries

**Patterns to check:**

```csharp
// BAD: Swallowed exception
try { } catch (Exception) { }

// BAD: Generic catch
try { } catch (Exception ex) { throw; }

// GOOD: Specific handling
try { }
catch (DbUpdateException ex) when (ex.IsUniqueViolation())
{
    throw new ConflictException("Entity already exists");
}
```

### Step 5: Async/Await Review

Check async patterns:

- [ ] No `.Result` or `.Wait()`
- [ ] Async methods end with `Async`
- [ ] CancellationToken accepted and forwarded
- [ ] Proper async disposal (`await using`)
- [ ] No async void (except event handlers)

**Patterns to check:**

```csharp
// BAD: Blocking call
var user = GetUserAsync(id).Result;

// GOOD: Async all the way
var user = await GetUserAsync(id, ct);
```

### Step 6: Null Safety Review

Check null handling:

- [ ] Nullable reference types used correctly
- [ ] Guard clauses for parameters
- [ ] No null suppression without justification
- [ ] Proper null checks

**Patterns to check:**

```csharp
// BAD: Null suppression without reason
var user = await context.Users.FindAsync(id);
return user!.Name;  // Can throw!

// GOOD: Proper null check
var user = await context.Users.FindAsync(id);
if (user is null)
    throw new NotFoundException("User", id);
return user.Name;
```

### Step 7: SOLID Principles Review

Check design principles:

- [ ] Single responsibility maintained
- [ ] Open/closed principle followed
- [ ] Interface segregation applied
- [ ] Dependency inversion followed
- [ ] No god classes

### Step 8: Code Smells

Check for common issues:

- [ ] No magic numbers/strings
- [ ] No dead code
- [ ] No duplicate code
- [ ] No overly long methods
- [ ] No deeply nested conditionals
- [ ] Proper use of constants

### Step 9: Generate Report

Output a structured review report:

```markdown
# Code Review Report

**Target**: {path}
**Date**: {date}
**Reviewer**: code-reviewer agent

## Summary
{Overall assessment}

## Must Fix
{Issues that block approval}

## Should Fix
{Issues that should be addressed}

## Consider
{Suggestions for improvement}

## Positive Feedback
{What's done well}
```

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `architecture-conventions` | Layer boundaries, feature folders |
| `csharp-conventions` | C# 14 standards |
| `api-conventions` | API patterns |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `code-reviewer` | Primary code analysis |
| `software-architect` | Architecture validation |

## Context7 Usage

Query libraries when checking API usage:
- `/dotnet/aspnetcore` - ASP.NET Core patterns
- `/efcore/efcore` - EF Core patterns

## Example

```bash
/review src/Authra.Application/Tenants/
```

**Outputs:**

```markdown
# Code Review Report

**Target**: src/Authra.Application/Tenants/
**Date**: 2026-01-25

## Summary
Good implementation following Clean Architecture.
Found 2 issues to address and 3 suggestions.

## Must Fix

### 1. Missing CancellationToken
**File**: ITenantService.cs:15
**Issue**: `GetAllAsync` doesn't accept CancellationToken

```csharp
// Current
Task<PagedResponse<TenantDto>> GetAllAsync(int limit, string? cursor);

// Should be
Task<PagedResponse<TenantDto>> GetAllAsync(int limit, string? cursor, CancellationToken ct);
```

## Should Fix

### 1. Inconsistent Async Naming
**File**: TenantService.cs:45
**Issue**: `MapToDto` should be synchronous or renamed

## Consider

### 1. Use Collection Expression
**File**: TenantService.cs:78
**Issue**: Can simplify array creation

```csharp
// Current
var roles = new List<string> { "admin", "member" };

// Suggested
List<string> roles = ["admin", "member"];
```

### 2. Extract Mapping Logic
**File**: TenantService.cs:89
**Issue**: Consider extracting DTO mapping to extension method

### 3. Add Summary Documentation
**File**: ITenantService.cs
**Issue**: Public interface methods could benefit from XML docs

## Positive Feedback
- Clean separation of DTOs and validators
- Proper use of records for DTOs
- Good use of primary constructors
- Consistent naming conventions
- Feature folder organization followed
```

## Checklist

Code review should verify:

- [ ] Clean Architecture boundaries respected
- [ ] C# 14 conventions followed
- [ ] Error handling appropriate
- [ ] Async patterns correct
- [ ] Null safety maintained
- [ ] SOLID principles applied
- [ ] No obvious code smells
- [ ] Tests exist (if applicable)
