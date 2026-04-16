---
name: new-component
description: "Workflow for adding a new component, module, or class to Authra — pick the right layer, follow single-type-per-file, wire up DI, add tests."
when_to_use: "When the user runs `/new-component` or asks to add a new class, service, value object, or module to the Authra solution."
argument-hint: "<ComponentName> [--layer Domain|Application|Infrastructure|Api]"
version: 2.2.0
---

# New Component: $ARGUMENTS

Adds a new component following Clean Architecture boundaries and Authra's one-type-per-file rule.

## Steps

### 1. Determine Location

Pick the correct layer based on what the component does:

| Layer | Project | What belongs here |
|-------|---------|-------------------|
| Domain | `src/Authra.Domain` | Entities, value objects, domain-level enums, domain exceptions |
| Application | `src/Authra.Application` | Service interfaces, DTOs, validators, command/query handlers, application exceptions |
| Infrastructure | `src/Authra.Infrastructure` | Service implementations, EF Core configuration, external adapters |
| Api | `src/Authra.Api` | Minimal API endpoint handlers, middleware, filters, Program wiring |

If the answer isn't obvious, consult `architecture-conventions`.

Check for similar existing components first — don't duplicate what exists.

### 2. Create the File

- One public type per file (see `project-conventions`)
- File name matches the type: `TokenService.cs` contains `public class TokenService`
- Place in the feature folder — e.g., `Authra.Application/Auth/ITokenService.cs`, not a flat `Services/` bucket

### 3. Implement

Follow `coding-conventions` and `csharp-conventions`:
- Primary constructor for DI (C# 14)
- Required members for non-nullable construction contracts
- `sealed` by default unless inheritance is designed in
- `internal` visibility for anything not part of the layer's public contract

### 4. Wire Up Dependency Injection

Register in the appropriate `AddX()` extension method:

- `Authra.Application.DependencyInjection.ApplicationServicesRegistration`
- `Authra.Infrastructure.DependencyInjection.InfrastructureServicesRegistration`
- API-layer services are wired up in `Authra.Api/Program.cs` or its feature-folder extension

Follow the DI organization convention (IHostApplicationBuilder, `ValidateOnStart` for options).

### 5. Add Tests

- Unit tests in `tests/Authra.UnitTests/` next to the corresponding layer folder
- Integration tests in `tests/Authra.IntegrationTests/` when the component touches persistence or the HTTP surface
- Follow `testing-conventions` for naming and AAA structure

### 6. Verify

- `dotnet build` — compile succeeds, no nullable warnings
- `dotnet test` — new tests pass, existing tests still pass
- `dotnet format` — style compliance

## When to Use Which Agent

- Implementation: `csharp-coder`
- Persistence / EF configuration: `persistence-specialist`
- Tests: `tester`
- Review: `code-reviewer`
