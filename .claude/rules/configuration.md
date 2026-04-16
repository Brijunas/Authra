---
name: configuration
description: Authra configuration and services — 1Password CLI for secrets (op run), Mailpit (dev) + Resend (prod) for email.
version: 2.2.0
paths:
  - "**/appsettings*.json"
  - "**/.env.*"
  - "**/Program.cs"
  - "src/Authra.Infrastructure/Services/*Email*.cs"
---

# Configuration

Secret management and email service. **Date**: 2026-01-25.

## Configuration Management — 1Password CLI

**Decision**: 1Password CLI with secret references.

### Strategy

| Environment | Method | Auth |
|-------------|--------|------|
| Local dev | `op run --env-file=.env.development` | Personal 1Password |
| CI/CD | GitHub Action + Service Account | Service Account token |
| Production | `op run --env-file=.env.production` | Service Account |

### Secret Reference Format

```
op://<vault>/<item>/<field>
```

### Environment Files (committed to git)

```bash
# .env.development
DATABASE_URL="op://Authra/dev-database/connection-string"
JWT_SIGNING_KEY="op://Authra/dev-jwt/private-key"
RESEND_API_KEY="op://Authra/dev-resend/api-key"
```

### Running the Application

```bash
# Local dev
op run --env-file=.env.development -- dotnet run

# Production
op run --env-file=.env.production -- dotnet Authra.Api.dll
```

### CI/CD (GitHub Actions)

```yaml
- uses: 1password/load-secrets-action@v2
  with:
    export-env: true
  env:
    OP_SERVICE_ACCOUNT_TOKEN: ${{ secrets.OP_SERVICE_ACCOUNT_TOKEN }}
    DATABASE_URL: op://Authra/prod-database/connection-string
```

### .NET Integration

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables();  // op run injects secrets here
```

### Rationale

1. **Single tool**: Same pattern for dev, CI, prod
2. **Secrets never on disk**: Only references in env files (safe to commit)
3. **Team-friendly**: Share vault, everyone gets secrets automatically
4. **No cloud lock-in**: Can migrate to Vault/Azure KV later

**Trade-offs**: 1Password subscription required ($4/user/mo). `op run` wrapper needed to start app.

**Deferred to v1.1**: Azure Key Vault / AWS Secrets Manager integration. Secret rotation automation.

## Email Service — Mailpit (dev) + Resend (prod)

**Decision**: SMTP abstraction with Mailpit for dev and Resend for production.

### Strategy

| Component | Technology | Purpose |
|-----------|------------|---------|
| SMTP Client | MailKit 4.10.0 | Send emails via SMTP |
| Templates | Scriban 6.5.2 | Render email templates |
| Dev/Test | Mailpit (Docker) | Catch emails, web UI |
| Production | Resend | 3,000 free emails/month |

### Mailpit (Development)

```yaml
# docker-compose.yml
services:
  mailpit:
    image: axllent/mailpit
    ports:
      - "8025:8025"  # Web UI
      - "1025:1025"  # SMTP
```

- Web UI: `http://localhost:8025`
- SMTP: `localhost:1025` (no auth needed)

### Abstractions

```csharp
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}
```

### Implementations by Environment

| Environment | IEmailSender | SMTP Host |
|-------------|--------------|-----------|
| Development | `SmtpEmailSender` | Mailpit (localhost:1025) |
| Tests | `InMemoryEmailSender` | N/A |
| Production | `SmtpEmailSender` | Resend SMTP |

### MVP Templates

| Template | Trigger |
|----------|---------|
| `password-reset` | POST /auth/forgot-password |
| `tenant-invite` | POST /tenants/{id}/members/invite |

### Rationale

1. **Mailpit**: Docker-based, catches all emails locally, web UI to inspect
2. **Resend**: Generous free tier (3,000/month), SMTP support, no vendor lock-in
3. **SMTP abstraction**: Same code for dev/prod, swap providers via config
4. **MailKit**: Industry standard, MIT license, 150M+ downloads
5. **Scriban**: Sandboxed templates, no ASP.NET dependency

**Trade-offs**: External provider for production (mitigated: can self-host Postal later). Resend newer than SendGrid (mitigated: SMTP standard, easy to switch).

**Deferred to v1.1**: Email queue (background jobs), delivery tracking/webhooks, self-hosted email server (Postal).
