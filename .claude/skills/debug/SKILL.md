---
name: debug
description: "Structured debugging workflow for Authra — reproduce, isolate, investigate, hypothesize, fix, verify. Covers .NET exceptions, EF Core query issues, and PostgreSQL / RLS behavior."
when_to_use: "When the user runs `/debug` or asks to investigate a bug, crash, failing test, or unexpected behavior in the Authra system."
argument-hint: "<issue description>"
version: 2.2.0
---

# Debug: $ARGUMENTS

Systematic root-cause investigation for the Authra identity system.

## Steps

### 1. Reproduce

- Confirm the issue happens reliably — if intermittent, try to identify the trigger (tenant context, specific identifier, timing, concurrent request)
- Write down exact steps: endpoint, request payload, auth state, expected vs actual response
- Capture relevant logs, status codes, Problem Details responses, and — for DB issues — the SQL that actually executed (`dotnet ef dbcontext` log level `Information`, or Npgsql `IncludeErrorDetail=true`)

### 2. Isolate

- Smallest reproduction: shrink the input / remove unrelated fields
- Failing component: which layer raised the error? Domain exception → Application exception middleware → API response
- Recent changes: `git log --oneline -n 20 -- <affected-path>` and review the diffs
- Tenant-scoped? Check whether RLS context is being set correctly (`SET LOCAL app.current_tenant_id = ...`)

### 3. Investigate

- Read the code paths end-to-end — do not guess at what a method does
- Add `ILogger` scoped logging at suspect boundaries; Serilog structured fields make this searchable
- For EF Core issues: enable `LogTo(Console.WriteLine, LogLevel.Information)` locally, or check the Testcontainers log stream
- For PostgreSQL issues: `EXPLAIN (ANALYZE, BUFFERS)` the failing query
- For JWT / auth issues: decode the token at `jwt.io`, check `tid` / `mid` / `oid` / `exp`
- For tests: run the single failing test with verbose output (`dotnet test --filter "FullyQualifiedName~..." --logger "console;verbosity=detailed"`)

### 4. Hypothesize

Form a specific, testable theory about the root cause:
- "Tenant ID isn't propagating because `TenantContextAccessor` is registered as singleton instead of scoped"
- "Refresh token rotation fails because the DB unique constraint fires before the old row is deleted in the same transaction"

Avoid vague theories ("probably a race condition") — keep refining until you can predict what would fix it.

### 5. Fix

- Minimal change that addresses the root cause — don't refactor around it
- No "defensive" code added beyond what fixes the bug — trust the rest of the system
- Preserve the public contract unless the contract itself is wrong
- If the fix exposes a secondary issue, file it separately — scope discipline

### 6. Verify

- Confirm the original reproduction now passes
- Run the full affected test suite (`dotnet test --filter "Category=...")` or the module): no regressions
- Add a **regression test** — this is the single highest-value deliverable of the debug session
- Check nearby code paths that depend on the same assumption

## Common Authra Bug Patterns

- **RLS misses**: tenant context not set before the query ran → result set is empty even though data exists
- **UUID v7 collisions in tests**: using fake IDs with the same timestamp region — use `Medo.Uuid7.Uuid7.NewGuid()` in test data builders
- **Argon2id verification fails**: parameters (memory, iterations, parallelism) changed but the stored hash still uses the old params — the `PasswordAuth.Params` column must be used
- **JWT clock skew**: ES256 signature validation fine but `exp`/`nbf` fails — check clock skew tolerance (default 5 min)
- **Refresh token reuse detection firing in tests**: the test DB isn't reset between cases — check Respawn / fixture lifecycle

## When to Use Which Agent

- Security bug: `security-analyzer`
- Performance regression: `performance-analyzer`
- Architectural / design bug: `software-architect`
- Straightforward code fix: `csharp-coder`
- Query plan / schema issue: `persistence-specialist`
