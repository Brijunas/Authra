---
name: coding-conventions
description: "General code-quality principles for Authra — small focused functions, single responsibility, composition over inheritance, minimal comments, fail-fast validation. Language-specific rules live in `csharp-conventions`."
when_to_use: "When writing or editing source code anywhere in the Authra solution. Applied alongside `csharp-conventions` and `architecture-conventions`."
paths:
  - "src/**/*"
  - "tests/**/*"
version: 2.2.0
---

# Coding Conventions

Universal coding principles applied across the Authra solution. Language-specific C# rules live in `csharp-conventions`. Layer-boundary rules live in `architecture-conventions`.

## General Principles

- Keep functions short and focused — one reason to change
- Single Responsibility Principle — one concept per class
- Composition over inheritance — prefer interfaces and DI injection over base classes
- Self-documenting code — named identifiers over comments
- Fail fast — validate at system boundaries (API endpoints, service entry points), trust internal code

## Code Style

- **Formatter**: `dotnet format` — run before committing. Trailing whitespace, import order, and brace style are enforced by `.editorconfig`
- **Max line length**: no hard cap; prefer line breaks over horizontal scrolling
- **Indentation**: 4 spaces (C#), 2 spaces (JSON / YAML)
- **File naming**: matches the public type (one public type per file — see `project-conventions`)

## Patterns to Follow

- **Guard clauses** at the top of methods over nested `if` pyramids
- **Null checks** via pattern matching: `if (x is null)` — not `if (x == null)`
- **Early return** on invalid state; keep the happy path unindented
- **Immutable data** where possible — `record`, `readonly record struct`, `init` setters
- **Dependency injection** via constructor parameters — no service locator / static singletons
- **Async all the way** — no `.Result` / `.Wait()` / `Task.Run(async ...).GetAwaiter().GetResult()`

## Anti-Patterns to Avoid

- Swallowing exceptions with empty `catch` blocks
- Catching `Exception` broadly — catch the specific type and let others propagate
- Wrapping exceptions without preserving the inner exception
- Mutating DTOs after they cross a layer boundary
- Magic strings / magic numbers — extract to named constants
- Deep inheritance hierarchies (>2 levels) — refactor to composition
- Regions (`#region`) — if a file needs regions, it should be split

## Error Handling

- Throw domain-specific exceptions from the Application layer (see `src/Authra.Application/Exceptions/`) — not `InvalidOperationException` / `ArgumentException` from business logic
- API-layer exception middleware converts to RFC 9457 Problem Details (see `api-conventions`)
- Never log and re-throw — pick one or the other
- Never catch `OperationCanceledException` without awareness — it's the normal cancellation signal

## Comments

- Default to **no comments** — the code should read itself
- Write one only when the WHY is non-obvious: a hidden constraint, a subtle invariant, a workaround for an upstream bug
- XML doc comments (`///`) are expected on public APIs in `Authra.Application` — document the contract, not the implementation
- Never leave `// TODO:` without a tracking reference (issue / task ID)
