---
name: git-conventions
description: "Git workflow for Authra — Conventional Commits, trunk-based development on master, branch naming, PR format."
when_to_use: "When committing, creating branches, or opening PRs."
version: 2.2.0
---

# Git Conventions

## Commit Messages

Conventional Commits format:

```
type(scope): short imperative description

[optional body explaining WHY, not WHAT]

[optional footer — breaking changes, co-authors, issue refs]
```

**Types:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `build`, `ci`, `style`

**Scope** (optional, single word in parens): the area touched — e.g., `auth`, `tenants`, `migrations`, `api`, `docker`, `claude`

**Examples:**

```
feat(auth): add refresh token rotation with reuse detection
fix(tenants): honor RLS on member listing
refactor(di): consolidate service registration into HostApplicationBuilder extensions
chore(claude): canonicalize agent and skill names per v2.2.0 templates
```

**Rules:**
- Subject line ≤ 72 chars, lowercase, no trailing period
- Use imperative mood ("add", "fix", "remove") — not past or present participle
- Body wrapped at ~72 chars
- Explain WHY in the body when the title isn't enough
- Reference issues in the footer: `Closes #42`

## Branch Naming

- `feature/<short-description>` — new functionality
- `fix/<short-description>` — bug fixes
- `refactor/<short-description>` — non-behavioral restructuring
- `chore/<short-description>` — tooling, config, dependencies

Kebab-case; keep under 50 chars; no ticket IDs required (but allowed).

## Workflow

Trunk-based on `master`:

1. Branch from `master`
2. Keep branches short-lived — rebase onto `master` before opening a PR
3. Squash on merge (unless the history is genuinely valuable to preserve)
4. Delete the branch after merge

## Pull Requests

**Title:** same format as a commit subject — it becomes the squash-merge commit title.

**Body:**
- Summary (1-3 bullets) — what changed and why
- Test plan — checklist of what was verified
- Breaking changes (if any) — highlight explicitly
- Screenshots for UI changes

## Things NOT to Do

- Never commit secrets (`.env`, `appsettings.Development.json`, `*.pfx`, `*.p12`) — these are in `.gitignore` for a reason
- Never `git push --force` to `master`
- Never amend a commit that's been pushed to a shared branch
- Never skip pre-commit hooks with `--no-verify` unless you have explicit authorization
- Never use `git add -A` / `git add .` without reviewing — sensitive files can slip in
