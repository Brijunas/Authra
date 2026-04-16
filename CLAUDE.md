# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Purpose

Authra — a multi-tenant identity and authentication system. Currently moving from design to early implementation: MVP schema, endpoints, and services are being built against the decisions documented in `.claude/rules/` and the companion `CLAUDE-*.md` spec files.

## Current Design Context

Multi-tenant identity system with:
- Global identity + authentication (username + password initially)
- Tenant onboarding (create or join tenants)
- Tenant administration (accounts, organizations, roles, permissions)
- Organizations within tenants; accounts can access multiple organizations
- Future third-party auth (Google/Apple/Microsoft)

## Project Conventions

Architectural decisions, patterns, and conventions live in `.claude/rules/`. Rule files with a `paths:` frontmatter auto-load when Claude reads matching files.

| Rule file | Scope | Topics |
|-----------|-------|--------|
| `.claude/rules/architecture-decisions.md` | Global | Identity model (Proposal B), PostgreSQL, permission naming, data model summary, session/tokens, REST API summary |
| `.claude/rules/tech-stack.md` | Global | NuGet packages, .NET 10, Docker Chiseled, testing stack |
| `.claude/rules/project-structure.md` | `src/**`, `tests/**` | Clean Architecture layout, layer responsibilities, DI pattern |
| `.claude/rules/security-conventions.md` | Api / Auth / security services | UUID v7, ID prefixes, Argon2id, JWT ES256 |
| `.claude/rules/api-design.md` | Api endpoints / validators / DTOs | RFC 9457 errors, FluentValidation, REST conventions, cursor pagination |
| `.claude/rules/data-model.md` | Infrastructure / Domain | Entities, RLS, EF Core UUID v7 defaults |
| `.claude/rules/configuration.md` | appsettings / env / Program.cs / Email | 1Password CLI (`op run`), Mailpit + Resend |

## Companion Documents

- [CLAUDE-DATA-MODEL.md](./CLAUDE-DATA-MODEL.md) — full 20-entity MVP schema
- [CLAUDE-SESSION-TOKENS.md](./CLAUDE-SESSION-TOKENS.md) — JWT + refresh token deep dive
- [CLAUDE-API.md](./CLAUDE-API.md) — full REST API specification (39 endpoints)
- [CLAUDE-RESEARCHED.md](./CLAUDE-RESEARCHED.md) — research, industry comparisons, rejected alternatives

## Available Custom Agents

Ten specialized agents in `.claude/agents/` are automatically available via the Task tool. **Proactively use these agents when the task matches their expertise — do not wait for explicit user request.**

### Architecture & Research

| Agent | Purpose | Model |
|-------|---------|-------|
| `software-architect` | System design validation, pattern evaluation, scalability analysis | opus |
| `research-analyst` | Information gathering, synthesis, web search | opus |
| `api-designer` | REST/GraphQL API design, OpenAPI documentation | opus |

### Development

| Agent | Purpose | Model |
|-------|---------|-------|
| `csharp-coder` | Feature implementation, endpoints, services, DTOs | sonnet |
| `persistence-specialist` | EF Core configuration, RLS, query optimization | opus |
| `migration-helper` | EF Core migrations, breaking-change review, expand/migrate/contract deploy plans | sonnet |
| `tester` | Unit and integration tests, Testcontainers, fixtures | sonnet |

### Review

| Agent | Purpose | Model |
|-------|---------|-------|
| `code-reviewer` | Code quality, conventions, architecture compliance | opus |
| `security-analyzer` | Security audit, auth/authz, cryptography, vulnerabilities | opus |
| `performance-analyzer` | N+1 queries, indexes, caching, async patterns | opus |

## Available Skills

### Convention Skills (auto-loaded via `paths:`)

Skills in `.claude/skills/` provide coding standards and patterns:

| Skill | Purpose |
|-------|---------|
| `coding-conventions` | General code-quality principles — function size, error handling, comment policy |
| `csharp-conventions` | C# 14 / .NET 10 coding standards, naming, patterns |
| `architecture-conventions` | Clean Architecture layer boundaries, feature folders, DI patterns |
| `api-conventions` | REST patterns, validation, error handling, pagination |
| `database-conventions` | Entity patterns, UUID v7, RLS, EF Core migrations |
| `testing-conventions` | xUnit v3, Testcontainers, Respawn, assertions |
| `security-practices` | JWT tokens, refresh tokens, Argon2id, permissions |
| `git-conventions` | Conventional Commits, branch naming, PR format |
| `project-conventions` | Solution layout, project naming, file conventions |

### Workflow Skills (slash commands)

| Command | Usage | Purpose |
|---------|-------|---------|
| `/new-feature` | `/new-feature <FeatureName>` | Create complete feature across all layers |
| `/new-endpoint` | `/new-endpoint "<METHOD> /v1/{path}"` | Add endpoint to existing feature |
| `/new-component` | `/new-component <Name>` | Add a new class / service / value object in the right layer |
| `/scaffold-entity` | `/scaffold-entity <EntityName>` | Create entity with Domain, EF config, migration |
| `/test-feature` | `/test-feature <FeatureName>` | Generate comprehensive test suite |
| `/debug` | `/debug <issue>` | Structured debugging workflow |
| `/refactor` | `/refactor <target>` | Safe, systematic refactoring workflow |
| `/review` | `/review <path>` | General code review |
| `/security-review` | `/security-review <path>` | Security audit of code |
| `/performance-check` | `/performance-check <path>` | Performance analysis |

### Typical Development Flow

```bash
# 1. Scaffold entity from data model
/scaffold-entity Organization

# 2. Scaffold feature with CRUD endpoints
/new-feature Organizations --entity Organization

# 3. Add custom endpoints
/new-endpoint "POST /v1/organizations/{id}/members" --permission "organizations:members.write"

# 4. Generate tests
/test-feature Organizations

# 5. Review before commit
/review src/Authra.Application/Organizations/
/security-review src/Authra.Api/Endpoints/Organizations/
/performance-check src/Authra.Infrastructure/Services/OrganizationService.cs
```

## Tool Usage

**Context7 MCP**: Always use Context7 MCP tools (`resolve-library-id` and `query-docs`) when working with library/API documentation, code generation, setup, or configuration steps. Use proactively without waiting for explicit user request.

### MCP Dependencies

| Agent | MCP Tools Required | Purpose |
|-------|-------------------|---------|
| `research-analyst` | WebFetch, WebSearch | Web research and information gathering |
| `persistence-specialist` | Context7 | EF Core and Npgsql documentation |
| `csharp-coder` | Context7 | ASP.NET Core and FluentValidation docs |
| `tester` | Context7 | xUnit, Testcontainers documentation |
| `security-analyzer` | Context7 | Security library documentation |
| `performance-analyzer` | Context7 | Performance optimization patterns |
| `code-reviewer` | Context7 | Library API verification |

**Context7 libraries used**: `/efcore/efcore`, `/npgsql/npgsql`, `/dotnet/aspnetcore`, `/fluentvalidation/fluentvalidation`, `/xunit/xunit`, `/testcontainers/testcontainers-dotnet`.

## Working with This Repository

### For Architecture Work

1. Read the decisions under `.claude/rules/` and the companion `CLAUDE-*.md` files
2. Use the `software-architect` agent for systematic architecture evaluation
3. Use the `research-analyst` agent for researching patterns, technologies, or industry practices
4. Use the `api-designer` agent for API contract design
5. Document new decisions with clear rationale and trade-off analysis

### For Implementation Work

1. Use `/scaffold-entity` to create entities from `CLAUDE-DATA-MODEL.md`
2. Use `/new-feature` to create complete features with all layers
3. Use `/new-endpoint` to extend features with new endpoints
4. Use `/new-component` to add a single class, service, or module
5. Use `/test-feature` to generate comprehensive tests
6. Use the `csharp-coder` agent for feature implementation
7. Use the `persistence-specialist` agent for schema and query design
8. Use the `migration-helper` agent when creating or reviewing EF Core migrations
9. Use the `tester` agent for test creation

### For Code Review

1. Use `/review` for general code quality review
2. Use `/security-review` for security-focused review
3. Use `/performance-check` for performance analysis
4. Use the `code-reviewer`, `security-analyzer`, and `performance-analyzer` agents for deeper audits

Run agents in parallel whenever their work is independent — never serialize what can be parallelized.

## Orchestrator-First Workflow

You are an **orchestrator first, coder second**. Delegate to agents and skills before doing work yourself.

### Priority

1. Can agents handle this? → Delegate (parallel when possible)
2. Can a skill handle this? → Invoke the skill
3. No agent or skill fits? → Do it yourself

### Parallel Execution

- Launch multiple agents simultaneously for independent tasks
- Use skills proactively when tasks match their purpose
- Never serialize what can be parallelized
