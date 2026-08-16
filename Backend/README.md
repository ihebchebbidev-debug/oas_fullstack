# Flowentra Backend (.NET 8 API)

The Flowentra backend is an ASP.NET Core 8 Web API providing the entire business logic layer (CRM, Sales, Inventory, Field Service, HR, Projects, Workflow, AI, Calendar, Website Builder, Dynamic Forms, etc.) for the Flowentra frontend. It is **multi-tenant** (database-per-tenant), uses **Entity Framework Core 8** against **PostgreSQL** (Neon), and exposes JSON over HTTPS plus **SignalR** hubs for realtime updates.

> See the repository root `README.md` for the high-level architecture and an overview of how the frontend and backend fit together. This document is the **deep-dive** for backend engineers.

---

## Table of Contents

1. [Tech Stack](#tech-stack)
2. [Project Layout](#project-layout)
3. [Local Development](#local-development)
4. [Configuration](#configuration)
5. [Multi-Tenancy](#multi-tenancy)
6. [Authentication & Authorization](#authentication--authorization)
7. [Database & Migrations](#database--migrations)
8. [Modules](#modules)
9. [Realtime (SignalR)](#realtime-signalr)
10. [Background Jobs](#background-jobs)
11. [File Uploads](#file-uploads)
12. [Email](#email)
13. [AI Integration](#ai-integration)
14. [Logging & Error Handling](#logging--error-handling)
15. [Caching](#caching)
16. [Swagger / OpenAPI](#swagger--openapi)
17. [Health Checks](#health-checks)
18. [Deployment](#deployment)
19. [Coding Conventions](#coding-conventions)
20. [Troubleshooting](#troubleshooting)

---

## Tech Stack

- **.NET 8** / **ASP.NET Core 8**
- **Entity Framework Core 8** + **Npgsql** (PostgreSQL provider)
- **Microsoft.AspNetCore.Authentication.JwtBearer** for JWT
- **System.IdentityModel.Tokens.Jwt** for token issuance
- **Microsoft.AspNetCore.SignalR** for realtime
- **Microsoft.AspNetCore.DataProtection** for encrypting OAuth tokens & secrets at rest
- **Swashbuckle.AspNetCore** for Swagger / OpenAPI
- **BCrypt.Net-Next** for password hashing
- **MailKit / MimeKit** for SMTP email
- **StackExchange.Redis** + **Microsoft.Extensions.Caching.StackExchangeRedis** (optional)

See `FlowServiceBackend.csproj` for the exact package versions.

---

## Project Layout

```
Backend/
├─ Program.cs                          # Composition root (DI, middleware, routes)
├─ FlowServiceBackend.csproj           # Project file (target: net8.0, AssemblyName: MyApi)
├─ appsettings.json                    # Default config (committed)
├─ appsettings.Development.json        # Dev overrides
├─ Dockerfile                          # Production container (mcr…/aspnet:8.0)
├─ render.yaml                         # Render deployment notes
├─ buildscript.ps                      # PowerShell build helper
│
├─ Configuration/                      # Cross-cutting setup
│  ├─ SwaggerConfiguration.cs          #   Bearer auth, tags, ordering
│  ├─ SwaggerFilters.cs                #   Operation / document filters
│  ├─ FileUploadOperationFilter.cs     #   multipart/form-data binding
│  └─ TokenHelper.cs                   #   JWT issuance helpers
│
├─ Infrastructure/                     # Technical concerns
│  ├─ ITenantEntity.cs                 #   Marker for tenant-scoped entities
│  ├─ TenantDbContextFactory.cs        #   Per-request DbContext per tenant
│  ├─ TenantSlugCache.cs               #   slug → connection string cache
│  ├─ GlobalExceptionMiddleware.cs     #   Uniform JSON error envelope
│  └─ Caching/…                        #   Cache abstractions & examples
│
├─ Data/                               # EF Core data layer
│  ├─ Migrations/                      #   Code-first EF migrations
│  └─ SeedData/                        #   Lookups, currencies, numbering seeds
│
├─ Database/                           # Raw SQL (canonical schema overrides)
│  ├─ cleanup_database.sql
│  └─ Migrations/*.sql
│
├─ Neon/                               # Numbered canonical schema scripts
│  └─ 01_*.sql … 29_*.sql              #   Run in order on a fresh DB
│
├─ Migrations/                         # Additional SQL + EF migrations
├─ Scripts/                            # One-off SQL utilities
│
├─ Modules/                            # Vertical slices (one folder per feature)
│  └─ <Feature>/
│     ├─ Controllers/                  #   REST endpoints
│     ├─ Services/                     #   Business logic + interfaces
│     ├─ Models/                       #   EF entities
│     ├─ DTOs/                         #   Request / response contracts
│     └─ Mappings/                     #   Entity ↔ DTO
│
└─ wwwroot/                            # Static (Swagger UI customisation, uploads)
   └─ swagger-ui/
```

---

## Local Development

### Prerequisites

- **.NET SDK 8.0.x** (`dotnet --info` should report `Microsoft.NETCore.App 8.x`)
- **PostgreSQL 14+** (a free **Neon** project works perfectly)
- *(optional)* **Redis 6+**

### Steps

```bash
cd Backend

# 1. Restore packages
dotnet restore

# 2. Configure (option A — appsettings.Development.json is already set up with a Neon dev DB)
#    OR set env vars (option B, preferred for production parity):
export ConnectionStrings__DefaultConnection="postgresql://USER:PASS@HOST/DB?sslmode=require"
export Jwt__Key="ChangeMe-AtLeast32CharsLongRandomKey1234"
export Jwt__Issuer="MyApi"
export Jwt__Audience="MyApiClients"

# 3. Apply schema (only on a fresh database)
for f in Neon/*.sql;       do psql "$DATABASE_URL" -f "$f"; done
for f in Migrations/*.sql; do psql "$DATABASE_URL" -f "$f"; done

# 4. Run
dotnet run                              # → http://localhost:5000
# or
dotnet watch run                        # hot reload
# or
dotnet run --launch-profile https       # → https://localhost:7000

# 5. Swagger
open http://localhost:5000/swagger
```

### Launch profiles

`Properties/launchSettings.json` defines:
- `http`  → `http://localhost:5000` (Swagger auto-opens)
- `https` → `https://localhost:7000;http://localhost:5000`
- `Production` → `http://0.0.0.0:10000` (Render-style)

---

## Configuration

Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` → environment variables → command-line.

### `appsettings.json` (committed defaults)

```jsonc
{
  "Jwt": {
    "Key": "YourSuperSecretKeyHere12345678901234567890",
    "Issuer": "MyApi",
    "Audience": "MyApiClients"
  },
  "OAuth": {
    "Google":    { "ClientId": "...", "ClientSecret": "...", "RedirectUri": "https://api.flowentra.app/oauth/google/callback" },
    "Microsoft": { "ClientId": "...", "ClientSecret": "...", "RedirectUri": "https://api.flowentra.app/oauth/microsoft/callback" }
  },
  "UploadThing": { "Token": "" },
  "Ollama":      { "BaseUrl": "http://localhost:11434", "DefaultModel": "mistral" },
  "Logging":     { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*"
}
```

### Environment variable mapping

ASP.NET Core uses the **double-underscore** convention to map nested keys:

| Setting key                                      | Env var                                          |
|--------------------------------------------------|--------------------------------------------------|
| `ConnectionStrings:DefaultConnection`            | `ConnectionStrings__DefaultConnection`           |
| `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience`        | `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience`     |
| `OAuth:Google:ClientId`                          | `OAuth__Google__ClientId`                        |
| `Redis:ConnectionString`                         | `Redis__ConnectionString`                        |
| `UploadThing:Token`                              | `UploadThing__Token`                             |
| `Smtp:Host` / `Smtp:Port` / `Smtp:User` / `Smtp:Pass` | `Smtp__Host` / `Smtp__Port` / etc.          |

### Per-tenant database URLs

Add **one env var per tenant** using the exact pattern:

```
TENANT_<TENANT_SLUG_UPPERCASE>_DATABASE_URL=postgresql://user:pass@host:5432/<db>?sslmode=require
```

Examples:

```
TENANT_DEMO_DATABASE_URL=postgresql://flowkyn:***@host:5432/DemoFlowentra?sslmode=require
TENANT_DEV_DATABASE_URL=postgresql://flowkyn:***@host:5432/DevFlowentra?sslmode=require
TENANT_KROSSIER_DATABASE_URL=postgresql://neondb_owner:***@host:5432/neondb?sslmode=require&channel_binding=require
```

If a tenant-specific var is not present, the API falls back to `ConnectionStrings__DefaultConnection`.

---

## Multi-Tenancy

Strategy: **database-per-tenant** with a shared application instance.

### Tenant Resolution Order (per request)

1. `X-Tenant` HTTP header (set by the frontend Axios interceptor).
2. JWT `tenant` claim (validated against the header — mismatch → `403`).
3. Subdomain (`<slug>.flowentra.app`) for browser navigations.
4. Fallback: `ConnectionStrings:DefaultConnection`.

### Implementation

- `Infrastructure/ITenantEntity.cs`
  ```csharp
  public interface ITenantEntity { int TenantId { get; set; } }
  ```
  Every tenant-scoped entity implements this. **Exceptions**: `MainAdminUser`, `Tenant` (root catalog).

- `Infrastructure/TenantSlugCache.cs` — process-wide cache of `slug → connection string` to avoid repeated env-var lookups.

- `Infrastructure/TenantDbContextFactory.cs` — DI-registered as **scoped**. On each request, it:
  1. Reads the resolved tenant slug.
  2. Looks up the connection string (`TENANT_<SLUG>_DATABASE_URL` env var → cached).
  3. Builds a `DbContextOptions<AppDbContext>` and returns an `AppDbContext` bound to that DB.

- EF Core **Global Query Filters** add `WHERE "TenantId" = @current` automatically on every query against tenant entities.

- `AppDbContext.SaveChangesAsync` overrides stamp `TenantId` on inserted entities so application code never has to set it manually.

### Adding a new tenant

1. Create a new Postgres database (e.g. on Neon).
2. Run `Neon/*.sql` then `Migrations/*.sql` against it.
3. Add `TENANT_<NEW_SLUG>_DATABASE_URL=...` to the backend host env.
4. Insert a row in the *root* `Tenants` table (slug, display name).
5. Restart (or wait for the slug cache TTL) and the new tenant is live.

---

## Authentication & Authorization

### Endpoints

| Method | Route                              | Purpose                                   |
|--------|------------------------------------|-------------------------------------------|
| POST   | `/api/auth/register`               | Create a new local user (if enabled)      |
| POST   | `/api/auth/login`                  | Email + password → JWT + refresh token    |
| POST   | `/api/auth/refresh`                | Rotate refresh token                      |
| POST   | `/api/auth/logout`                 | Revoke refresh token                      |
| POST   | `/api/auth/forgot-password`        | Send reset email                          |
| POST   | `/api/auth/reset-password`         | Apply new password using emailed token    |
| GET    | `/oauth/google/login`              | Begin Google OAuth                        |
| GET    | `/oauth/google/callback`           | Google OAuth callback (mints JWT)         |
| GET    | `/oauth/microsoft/login`           | Begin Microsoft OAuth                     |
| GET    | `/oauth/microsoft/callback`        | Microsoft OAuth callback (mints JWT)      |

### JWT

- Algorithm: **HS256**, signed with `Jwt:Key` (≥ 32 chars).
- Claims: `sub`, `email`, `tenant`, `roles[]`, `permissions[]`, `iat`, `exp`.
- Lifetime: 60 min (configurable). Paired with a long-lived refresh token (rotation, single-use).
- Validation parameters configured in `Program.cs` (issuer, audience, signing key, clock skew = 0).

### Roles & Permissions

- **Roles** stored in `Roles` table (per-tenant), assignments in `UserRoles`.
- **Fine-grained permissions** stored in `RolePermissions` (e.g. `articles.read`, `articles.write`, `sales.delete`).
- Controller / action level: `[Authorize(Roles = "Admin")]` or `[HasPermission("articles.write")]` (custom policy).
- The frontend mirrors these via the `permissionBroadcast` utility for UI gating.

### Password storage

- BCrypt with cost factor **11** (`BCrypt.Net.BCrypt.HashPassword(pwd, 11)`).
- No password ever leaves the database in plaintext or via API responses.

---

## Database & Migrations

### Source of truth

- **`Backend/Neon/*.sql`** — canonical schema, numbered scripts, run on a fresh DB in order.
- **`Backend/Database/Migrations/*.sql`** — incremental raw SQL changes.
- **`Backend/Migrations/*.sql`** + **`*.cs`** — additional / EF-generated changes.
- **`Backend/Data/Migrations/`** — EF code-first migrations directory (used during dev).

### Applying everything to a brand-new database

```bash
# Reset (DESTRUCTIVE — only on disposable dev DBs)
psql "$DATABASE_URL" -f Database/cleanup_database.sql

# Canonical schema
for f in Neon/*.sql; do psql "$DATABASE_URL" -f "$f"; done

# Subsequent SQL migrations
for f in Database/Migrations/*.sql; do psql "$DATABASE_URL" -f "$f"; done
for f in Migrations/*.sql;          do psql "$DATABASE_URL" -f "$f"; done
```

### Code-first EF migrations

```bash
# Create a new migration
dotnet ef migrations add <Name> --project FlowServiceBackend.csproj --output-dir Data/Migrations

# Apply pending migrations to the configured DB
dotnet ef database update
```

> Production deployments should prefer **idempotent SQL scripts** in `Migrations/` to avoid surprises across tenants.

### Schema reference

Full table-by-table column docs (PK / FK / NN / type / source migration) are generated into `src/modules/settings/data/dbTables.ts` and rendered inside the running app:

> **Settings → Backend Documentation → Database**

This covers all 67 tables / 963 columns currently in the schema.

---

## Modules

Each feature is implemented as a **vertical slice** under `Backend/Modules/<Feature>/`:

```
Modules/Articles/
├─ Controllers/ArticlesController.cs
├─ Services/IArticleService.cs
├─ Services/ArticleService.cs
├─ Models/Article.cs                  # EF entity
├─ Models/ArticleNote.cs
├─ DTOs/ArticleDto.cs
├─ DTOs/CreateArticleRequest.cs
└─ Mappings/ArticleMappings.cs
```

### Module catalog

| Folder                  | Domain                                                      |
|-------------------------|-------------------------------------------------------------|
| `AiChat`                | AI chat history & assistant orchestration                   |
| `Articles`              | Materials & services catalog, stock metadata                |
| `Auth`                  | Login, register, refresh, OAuth, password reset             |
| `Calendar`              | Events, recurrences, synced calendars                       |
| `Contacts`              | CRM contacts, addresses, geolocation                        |
| `Dashboards`            | User-defined dashboards & widgets                           |
| `Dispatches`            | Field dispatch board, jobs, status updates                  |
| `Documents`             | File library, compression, public sharing                   |
| `DynamicForms`          | Form builder, public submissions, thank-you flows           |
| `EmailAccounts`         | Connected mailboxes (IMAP/SMTP/OAuth) + sync                |
| `ExternalEndpoints`     | Tenant-managed external API endpoints (webhooks, callbacks) |
| `HR`                    | Employees, contracts, attendance, leave                     |
| `Installations`         | Field installations and material tracking                   |
| `Lookups`               | Lookup catalogs (categories, statuses, types, …)            |
| `Notifications`         | In-app, email, push notifications                           |
| `Numbering`             | Document numbering rules & sequences                        |
| `Offers`                | Commercial offers, items, sending stats, e-sign             |
| `OfflineHydration`      | Per-user offline preload preferences                        |
| `Payments`              | Payments tracking, Stripe webhooks                          |
| `Planning`              | Scheduling & planning board                                 |
| `Plugins`               | Tenant plugin activations                                   |
| `Preferences`           | Tenant + user preferences                                   |
| `Projects`              | Projects, tasks, checklists, time entries, activity, notes  |
| `Purchases`             | Supplier orders, goods receipts                             |
| `RetenueSource`         | Withholding tax (RS) compliance                             |
| `Roles`                 | Roles, permissions, role assignments                        |
| `Sales`                 | Sales / Invoices, items, attachments, tax types             |
| `ServiceOrders`         | Service orders + materials, installation flow               |
| `Settings`              | Tenant settings, branding, fiscal stamp, currency           |
| `Shared`                | Shared infra (paging, filtering, base classes)              |
| `Signatures`            | Electronic signatures                                       |
| `Skills`                | Technician skills catalog                                   |
| `SupportTickets`        | Tickets, comments, attachments                              |
| `Sync`                  | Offline sync push/pull/history/retry                        |
| `Tenants`               | Root tenant catalog management                              |
| `UserAiSettings`        | Per-user AI keys & preferences                              |
| `Users`                 | User CRUD, profile picture, invitations                     |
| `WebsiteBuilder`        | Sites, pages, sections, media                               |
| `WorkflowEngine`        | Visual workflow definitions, executions, processed entities |

### Adding a new module

1. Create `Backend/Modules/<Feature>/{Controllers,Services,Models,DTOs}/`.
2. Implement EF entity → register in `AppDbContext.OnModelCreating`.
3. Add a SQL migration under `Backend/Migrations/` for production.
4. Implement service interface + class, register in DI (`Program.cs` or a module-level `*Extensions.cs`).
5. Add controller with `[ApiController]`, `[Route("api/<feature>")]`, `[Authorize]`.
6. Add Swagger annotations (`[SwaggerOperation]`, `[ProducesResponseType]`).
7. Add corresponding frontend module under `src/modules/<feature>/`.

---

## Realtime (SignalR)

Hubs are mapped in `Program.cs`:

| Hub path                | Purpose                                          |
|-------------------------|--------------------------------------------------|
| `/hubs/notifications`   | Push notifications, toast events                 |
| `/hubs/dispatch`        | Live dispatcher board (jobs, technicians)        |
| `/hubs/sync`            | Offline sync events (push / pull progress)       |

Authentication is via the same JWT — clients pass it via the `accessTokenFactory` option of the SignalR JS client. Tenant scoping is enforced server-side.

---

## Background Jobs

Implemented as `IHostedService` background workers:

- **WebhookForwardingWorker** — picks up `WebhookForwardJobs` rows (outbox), POSTs to the target URL, retries with exponential backoff.
- **LowStockNotificationWorker** — scans `Articles` periodically, emits notifications when stock ≤ minStock.
- **NumberingMaintenanceWorker** — resets sequences according to the configured period.
- **SyncQueueProcessor** — processes pending offline sync entries.

Each worker respects multi-tenancy by iterating over registered tenants and opening per-tenant `DbContext`s.

---

## File Uploads

Two paths, depending on file size & destination:

1. **Direct API upload** (multipart/form-data) — small files, stored under `Backend/wwwroot/uploads/<scope>/`. The Swagger filter `FileUploadOperationFilter.cs` makes Swagger UI render proper file pickers.
2. **UploadThing** — frontend uploads directly to UploadThing CDN, then sends back the URL to the API which persists it on the entity (e.g. `Articles.imageUrl`, `CompanyLogoUrl`, `ProfilePictureUrl`).

Static files are served from `wwwroot/` via `app.UseStaticFiles()`.

---

## Email

- SMTP via **MailKit / MimeKit**.
- Configure `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Pass`, `Smtp__From`.
- Templates under `Modules/Notifications/` and `Modules/Auth/` (welcome, reset, magic-link, document-shared, etc.).
- Per-tenant **Custom Email Accounts** (`CustomEmailAccounts` table) allow tenants to send mail from their own SMTP (encrypted at rest with DataProtection keys persisted under `/app/keys`).

---

## AI Integration

- **OpenRouter** — proxy to many model providers; per-user API key stored encrypted in `UserAiSettings`.
- **Ollama** — local model fallback (default `mistral`), configured via `Ollama:BaseUrl`.
- The `AiChat` module persists chat history per user in `AiChatHistory` and exposes streaming endpoints over SignalR.

---

## Logging & Error Handling

- Built-in `ILogger<T>` everywhere; log levels per category set in `appsettings*.json`.
- `Infrastructure/GlobalExceptionMiddleware.cs` catches unhandled exceptions and returns a consistent JSON envelope:

  ```json
  {
    "error": {
      "code": "ARTICLE_NOT_FOUND",
      "message": "Article 42 not found",
      "traceId": "00-abc..."
    }
  }
  ```

- Domain exceptions inherit from `AppException` and carry an HTTP status + error code.
- Persistent application logs are also written to the `SystemLogs` table (per tenant) when configured.

---

## Caching

- Default: **in-memory** (`IMemoryCache`).
- If `Redis:ConnectionString` is set, **distributed Redis** is used instead (and SignalR backplane optionally).
- Examples and abstractions under `Infrastructure/Caching/`.
- `TenantSlugCache` (process-wide) avoids repeated env-var lookups on hot paths.

---

## Swagger / OpenAPI

- Available at `/swagger` (UI) and `/swagger/v1/swagger.json` (raw OpenAPI).
- Customised via:
  - `Configuration/SwaggerConfiguration.cs` — bearer auth, server URLs, document grouping.
  - `Configuration/SwaggerFilters.cs` — operation/document filters (tags, ordering, examples).
  - `Configuration/FileUploadOperationFilter.cs` — multipart binding for file endpoints.
  - `wwwroot/swagger-ui/custom.css` + `dev-token.js` — branded UI + a dev-only "Use last token" helper.
- XML documentation comments are emitted (`GenerateDocumentationFile` in `.csproj`) so descriptions appear in Swagger automatically.

---

## Health Checks

- `GET /health` — returns `200 OK` when the API process is up. Used by the Docker `HEALTHCHECK` and Render's health probe.
- (Optional) extend with EF Core / Redis health checks via `Microsoft.Extensions.Diagnostics.HealthChecks`.

---

## Deployment

### Render (recommended)

See `Backend/render.yaml` for required env vars. Build/start:

```
Build:  dotnet publish -c Release -o out
Start:  dotnet out/MyApi.dll
```

Required Render env vars:

```
DATABASE_URL=postgresql://...
ASPNETCORE_ENVIRONMENT=Production
PORT=10000                              # Render injects automatically
JWT_KEY=...
JWT_ISSUER=MyApi
JWT_AUDIENCE=MyApiClients
TENANT_<SLUG>_DATABASE_URL=...          # one per tenant
```

### Docker

```bash
cd Backend
docker build -t flowentra-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="postgresql://..." \
  -e Jwt__Key="..." \
  -e Jwt__Issuer="MyApi" \
  -e Jwt__Audience="MyApiClients" \
  -v $(pwd)/keys:/app/keys \
  flowentra-api
```

The container exposes port **8080** (HTTP) and **8081** (HTTPS). Healthcheck hits `/health`.

### Bare metal / IIS

`dotnet publish -c Release -o out` then run `dotnet out/MyApi.dll` behind a reverse proxy (nginx / IIS / Caddy). Enable HTTPS termination at the proxy.

---

## Coding Conventions

- **C# 12** features allowed (file-scoped namespaces, primary constructors, collection expressions).
- `Nullable` is **enabled** project-wide — annotate accordingly.
- `ImplicitUsings` is **enabled** — keep `using` blocks minimal.
- One service interface per feature (`IArticleService` + `ArticleService`).
- DTOs are immutable records where practical: `public record ArticleDto(int Id, string Name, ...)`.
- Use `async`/`await` for **all** DB and I/O calls; suffix with `Async`.
- Prefer **EF Core** LINQ over raw SQL; drop to raw SQL only for performance-critical paths.
- Always include the tenant filter check (rely on Global Query Filters; do not bypass via `IgnoreQueryFilters()` unless explicitly auditing across tenants).
- Add `[ProducesResponseType]` for every controller action so Swagger documents responses correctly.
- XML doc comments (`/// <summary>...`) on public APIs so they appear in Swagger.

---

## Troubleshooting

| Symptom                                            | Likely cause / fix                                                                 |
|----------------------------------------------------|------------------------------------------------------------------------------------|
| `relation "X" does not exist`                      | A SQL script was skipped — re-run `Neon/*.sql` then `Migrations/*.sql` in order    |
| `401 Unauthorized` on every endpoint               | Missing / expired JWT, or `Jwt:Key` mismatch between issuer & validator            |
| `403 Forbidden` after login                        | `X-Tenant` header doesn't match JWT `tenant` claim                                 |
| `Tenant connection string not found for slug 'X'`  | Add `TENANT_X_DATABASE_URL` env var, then restart (or wait for slug cache TTL)     |
| `Npgsql.PostgresException: 28P01`                  | Wrong DB password in connection string                                             |
| `An exception occurred while iterating over results of a query for context type 'AppDbContext'` | Connection string points to the wrong DB / pooler / region |
| OAuth callback `redirect_uri_mismatch`             | Registered URI in provider console must exactly match `OAuth:<Provider>:RedirectUri` |
| `413 Payload Too Large` on uploads                 | Increase Kestrel `Limits:MaxRequestBodySize` and reverse-proxy body limits         |
| Swagger UI shows no endpoints                      | Ensure controllers have `[ApiController]` + `[Route(...)]` and module is registered |
| SignalR connects then disconnects with `401`       | Pass JWT via `accessTokenFactory` and ensure CORS allows the frontend origin       |
| DataProtection warning about ephemeral keys        | Mount a persistent volume to `/app/keys` (Docker) or set `DataProtection:KeyPath`  |
| Slow queries on hot endpoints                      | Enable Redis (`Redis__ConnectionString`) and/or add EF `AsNoTracking()`            |

---

## License

Proprietary — © Flowentra. All rights reserved.
