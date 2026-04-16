---
name: refactor
description: "Safe, systematic refactoring workflow for Authra — assess, ensure coverage, plan, execute in small steps, verify no behavior change."
when_to_use: "When the user runs `/refactor` or asks to improve code structure without changing behavior. Not for adding features or fixing bugs."
argument-hint: "<target to refactor>"
version: 2.2.0
---

# Refactor: $ARGUMENTS

Behavior-preserving code improvement in small, verifiable steps.

## Steps

### 1. Assess

- What does the code currently do? Read it in full — don't assume
- What's the success criterion? (Better layer boundary, less duplication, easier to test, clearer naming)
- What must NOT change? Public APIs, DB schema, HTTP contracts, observable behavior

If you can't articulate the success criterion in one sentence, the refactor scope isn't clear yet — stop and clarify.

### 2. Ensure Coverage

**This is non-negotiable.** Refactoring without tests is editing.

- Are there existing tests covering the target? `dotnet test --filter "..."` — confirm they pass
- If coverage is thin, write **characterization tests** first — tests that lock down the current behavior, warts and all. These can be deleted later once refactoring is done
- Integration tests with Testcontainers are preferred over mocked unit tests for persistence / API code — they catch schema and RLS drift (see `testing-conventions`)

### 3. Plan

List the refactor as a sequence of small, behavior-preserving steps:

1. Extract method `A` from `B`
2. Move `A` to a new class `C`
3. Inject `C` via constructor
4. Delete the now-unused code in `B`

Each step should leave the tests green. If a step can't, break it down further.

### 4. Execute

- One step at a time
- Run tests after each step (`dotnet test` — filter to the affected project for speed)
- Commit after each green step — small commits make it easy to revert one step without losing the rest
- Commit message per step: `refactor(<scope>): extract X from Y` (see `git-conventions`)

### 5. Verify

- All tests still pass (`dotnet test` on the full solution)
- No behavior change: the refactor did not introduce or fix any bug — if it did, split the bug fix into a separate commit
- `dotnet build` — zero new warnings
- `dotnet format` — style clean

### 6. Clean Up

- Remove dead code (unused methods, unreachable branches, legacy shims) — they rot otherwise
- Update XML doc comments if public contracts changed even cosmetically
- Update relevant skill / rule files if a new pattern was introduced

## Anti-Patterns

- **Scope creep** — adding features or fixing bugs "while I'm in here". Don't. Those go in separate commits / PRs
- **Big-bang rewrite** — refactoring 20 files at once, tests broken throughout, hoping it compiles at the end. Tests must stay green at each step
- **Refactoring without tests** — see step 2
- **Renaming things for the sake of renaming** — only if the new name is materially clearer
- **Introducing abstractions for "flexibility"** — YAGNI. Add abstractions when a second concrete implementation exists, not before

## When to Use Which Agent

- Implementation: `csharp-coder`
- Architectural refactor (layer boundaries, service decomposition): `software-architect` first, then `csharp-coder`
- Schema / persistence refactor: `persistence-specialist` + `migration-helper`
- Post-refactor review: `code-reviewer`
