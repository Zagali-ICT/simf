# SIMF Developer Guide

| Field | Value |
|-------|-------|
| Document | SIMF Developer Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-22 (the Control Panel base shell) |
| Related | SIMF-SES-001, SIMF-SAD-001, SIMF-CPD-001, SIMF-VID-001 |

This guide explains how to build, run and work on the SIMF backend. It grows
one section per delivered increment.

## 1. Prerequisites

- .NET 10 SDK
- SQL Server 2022 — Standard edition for production; Express or LocalDB is
  enough for development (used from increment 2)
- Git

## 2. Solution layout

    SIMF.slnx                  The solution (the .NET 10 XML solution format)
    Directory.Build.props      Shared build settings for every project
    src/
      Backend/
        SIMF.Domain            Entities, value objects, domain rules
        SIMF.Application       Use cases, handlers, validators
        SIMF.Infrastructure    Persistence, external-service adapters
        SIMF.Api               The HTTP API (FastEndpoints) — the host
        SIMF.RealTime          SignalR hubs
      ControlPanel/
        SIMF.ControlPanel      The Control Panel — a Blazor Server app
      Website/
        SIMF.Web               The public Website — a Blazor SSR app
      Shared/
        SIMF.Contracts         Request and response DTOs
        SIMF.Common            ApiResult envelope, error model, shared constants
        SIMF.Components        The Simf* Blazor component library
        SIMF.ApiClient         The typed client for the Login API
    tests/
      SIMF.Domain.Tests
      SIMF.Application.Tests
      SIMF.Api.Tests
      SIMF.ApiClient.Tests

Project dependencies follow the DDD layering of SIMF-SAD-001 section 6 —
dependencies point inward: Application depends on Domain; Infrastructure on
Application and Domain; Api on Application, Infrastructure, RealTime, Contracts
and Common.

## 3. Build, run and test

| Action | Command |
|--------|---------|
| Build | `dotnet build SIMF.slnx -c Release` |
| Run the API | `dotnet run --project src/Backend/SIMF.Api` |
| Run the Control Panel | `dotnet run --project src/ControlPanel/SIMF.ControlPanel` |
| Run the Website | `dotnet run --project src/Website/SIMF.Web` |
| Test | `dotnet test SIMF.slnx -c Release` |

The Release build treats warnings as errors and must pass with zero warnings
(SIMF-SES-001 section 13).

## 4. Configuration

`appsettings.json` holds non-secret settings. `appsettings.Development.json`
holds development-only values and is committed, so a new developer has a working
local environment. Production secrets are never committed — they are supplied
through `set-env-*` scripts and `appsettings.Production.json` (SIMF-SES-001
section 4.4).

## 5. Increment 1 — the solution scaffold

Increment 1 establishes the solution and its cross-cutting pieces:

- **`ApiResult<T>` and `ApiError`** (`SIMF.Common`) — the standard response
  envelope of SIMF-API-001 section 6. Every endpoint returns this shape, so a
  client parses success and failure the same way every time.
- **`ErrorCodes`** (`SIMF.Common`) — the stable error-code catalogue; codes are
  constants, never string literals.
- **`ErrorHandlingMiddleware`** (`SIMF.Api`) — the first middleware in the
  pipeline; it catches any unhandled exception and returns the `ApiResult`
  error envelope, so no exception reaches a client as a raw stack trace.
- **`/health`** — the readiness endpoint. It reports liveness now; the database
  and migration checks are added in increment 2.
- **Serilog** — structured logging, configured from the `Serilog` section of
  `appsettings`.

FastEndpoints is referenced by `SIMF.Api` but wired in increment 3, with the
first endpoint — FastEndpoints requires at least one endpoint to start.

## 6. Increment 2 — the data layer

Increment 2 establishes the Identity & Access data foundation:

- **Domain entities** (`SIMF.Domain/IdentityAccess`) — `SimfUser` and `SimfRole`
  extend ASP.NET Core Identity; `Permission`, `RolePermission`, `RefreshToken`
  and `AccountCode` are SIMF's own entities, with the `AccountState` and
  `AccountCodePurpose` enums.
- **Two database contexts** (`SIMF.Infrastructure/Persistence`) —
  `SimfIdentityDbContext` (built on `IdentityDbContext`) holds the Identity &
  Access tables; `SimfAppDbContext` holds the business entities (none yet).
  Both target **one physical database** (decision C-1) and each keeps its own
  migration history table (`__EFMigrationsHistory_Identity` and
  `__EFMigrationsHistory_App`).
- **Repositories** — `IRefreshTokenRepository`, `IAccountCodeRepository` and
  `IPermissionRepository` (interfaces in `SIMF.Application`, implementations in
  `SIMF.Infrastructure`). Users and roles are reached through ASP.NET Identity's
  `UserManager` and `RoleManager`, so they need no custom repository.
- **`AddInfrastructure`** (`SIMF.Infrastructure/DependencyInjection.cs`) —
  registers both contexts (with connection pooling and `EnableRetryOnFailure`)
  and the repositories. It is wired into the API in increment 3.

### Database migrations

Each context has its own migration folder and a design-time factory, so a
migration is generated without running the API. For the Identity context:

    dotnet ef migrations add <Name> --project src/Backend/SIMF.Infrastructure
        --startup-project src/Backend/SIMF.Infrastructure
        --context SimfIdentityDbContext --output-dir Persistence/Migrations/Identity

(run as one command). Use `--context SimfAppDbContext` and `--output-dir
Persistence/Migrations/App` for the application context.

The development connection string (`ConnectionStrings:SimfDb` in
`appsettings.Development.json`) points at the local SQL Server instance;
production supplies it through the `set-env-*` script.

## 7. Increment 3 — account creation

Increment 3 adds the first API endpoints — account creation:

- **Endpoints** (`SIMF.Api/Endpoints/Auth`) — `POST /api/v1/auth/sign-up`,
  `verify-email` and `resend-code` (SIMF-API-001 section 12.4), built on
  FastEndpoints. The endpoints are thin: they call `RegistrationService` and
  return the `ApiResult<T>` envelope. Request validation uses FluentValidation
  (`Validator<T>` classes); the section 12.5 password policy is enforced there,
  in one place.
- **`RegistrationService`** (`SIMF.Application/IdentityAccess`) — the sign-up,
  verify-email and resend-code use cases. It creates the user through ASP.NET
  Identity's `UserManager`, issues a six-digit `AccountCode`, and queues the
  verification email. A self-registered account reaches `EmailVerified`; the
  registration profile and the approval workflow belong to SIMF-FDS-002.
- **Email pipeline** — `IEmailQueue` accepts a message; `EmailBackgroundService`
  drains the queue and `SmtpEmailSender` (MailKit) sends it. Sign-up never waits
  on the mail server (SIMF-SAD-001 Amendment A.2).
- **`IdentitySeeder`** — seeds the super-admin at startup (skipped under the
  test host). Supply `SuperAdmin:Email` and `SuperAdmin:TempPassword` through
  user-secrets or the `set-env` script; they are never committed.
- **Error handling** — `ErrorHandlingMiddleware` maps an `ApiException` to its
  declared error code and HTTP status, and any other exception to a 500.
- **Startup** — outside the test host, the API applies the EF migrations and
  runs the seeder before it serves requests.

Rate limiting on the authentication endpoints is part of the middleware
pipeline and is added in increment 4 (decision D-004).

## 8. Increment 4a — audit log, rate limiting, request context

Increment 4a adds the cross-cutting foundation for the sign-in feature:

- **Operation log** — `OperationLogEntry` (`SIMF.Domain/Auditing`) is the
  durable audit trail (SIMF-FDS-001 section 9). `IAuditLog` / `AuditLog` write
  an entry — with the source IP, user-agent and correlation id — to the
  `OperationLog` table in the application database. The account-creation
  endpoints record every outcome (`AuditEvents`).
- **Rate limiting** — a fixed-window limiter per client IP on the `/auth/*`
  endpoints (`429 RATE_LIMIT_EXCEEDED`), sized by the `RateLimit` configuration
  section. `RegistrationService` additionally caps verification-code resends
  per account.
- **Request context** — `CorrelationIdMiddleware` gives each request a
  correlation id (the `X-Correlation-Id` header) and pushes it into the log
  context; `ForwardedHeaders` recovers the real client IP behind the reverse
  proxy (the production known-proxy list is a deployment setting).

An unhandled 500 is logged through Serilog with its correlation id; it is not
written to the operation-log table (decision D-007).

**Reverse-proxy trust.** `X-Forwarded-For` is honoured only from an address in
`ReverseProxy:KnownProxies`. Outside Development and the test host that list
**must** be configured — the startup fails fast if it is empty — because the
rate limiter and the audit-log source IP depend on a trusted proxy. Logs are
written to the console and to a rolling daily file (`logs/`).

## 9. Increment 4b — sign-in and the second factors

Increment 4b adds sign-in:

- **Endpoints** (`SIMF.Api/Endpoints/Auth`) — `sign-in` (the password step),
  `verify-totp` (Control Panel) and `verify-otp` (visitors).
- **`SignInService`** — checks the password and the lockout state, then issues
  a short-lived `SecondFactorToken` ticket. A user holding any role is a
  Control Panel user and completes with an authenticator TOTP code; every other
  user with a code emailed to them. Tokens are issued only once the second
  factor passes.
- **`JwtTokenService`** — issues the HMAC-SHA256 access token (claims: subject,
  email, display name, the security stamp, roles). The signing key comes from
  the `Jwt` configuration section — supplied through `set-env`, never committed.
- **Refresh token** — a random opaque token, stored only as a hash; rotation
  and the refresh endpoint are increment 4c.
- **Lockout** — ASP.NET Core Identity lockout (5 failed attempts, 15 minutes).
- **Seeding** — `IdentitySeeder` now also seeds the **Administrator role** and
  assigns the super-admin to it, so the super-admin routes to the TOTP path.

The JWT bearer authentication middleware — validating the token on protected
endpoints — is increment 4c.

## 10. Increment 4c — refresh, sign-out, the authentication middleware

Increment 4c completes the session lifecycle:

- **`refresh`** (`SessionService`) — exchanges a refresh token for a new access
  token and **rotates** the refresh token (the old one is revoked, the new one
  chained by `RotatedFromId`). Presenting an already-revoked token is treated as
  a stolen-token signal: every session for the account is revoked and the event
  is audited `RefreshToken.Reused`.
- **`sign-out`** — requires a valid access token; revokes every refresh token
  for the caller and rolls the security stamp, ending all sessions (decision
  D-012).
- **JWT bearer authentication** — `Program.cs` validates the access token with
  hardened `TokenValidationParameters` (issuer, audience, lifetime, signing key;
  `HS256` pinned). `OnTokenValidated` compares the token's `security_stamp`
  claim to the account's current stamp and rejects a token from an ended
  session — so sign-out revokes live access tokens too.

The Login API is feature-complete at this increment except password
reset/change (increment 5).

## 11. Increment 5 — forgot, reset and change password

Increment 5 completes the Login API with password recovery:

- **Endpoints** (`SIMF.Api/Endpoints/Auth`) — `forgot-password` and
  `reset-password` (both anonymous), and `change-password` (requires sign-in).
- **`PasswordService`** — `forgot-password` issues a six-digit `PasswordReset`
  code (an `AccountCode`) and emails it; the response is the same whether or not
  the account exists, and a per-account re-issue cap stops inbox flooding.
  `reset-password` verifies the code and sets the new password;
  `change-password` verifies the current password first. Either one clears the
  forced-change flag and ends every session for the account.
- **The password policy** is the shared `PasswordRules.StrongPassword` validator
  rule (length, a digit, a letter) plus the ASP.NET Identity baseline (D-005).
- The new password is set with `RemovePasswordAsync` + `AddPasswordAsync` inside
  a transaction (decision D-014).

The Login API is feature-complete.

## 12. The frontend — Control Panel and Website login

This increment starts the two frontend applications and delivers their
authentication pages against the Login API.

- **`SIMF.ControlPanel`** — the Control Panel, a Blazor Server application
  (Interactive Server render mode). **`SIMF.Web`** — the public Website, a
  Blazor application rendered server-side, with the authentication pages as
  Interactive Server islands. The render-mode choice is decision D-017.
- **`SIMF.Components`** — the `Simf*` component library. Per the approved
  2026-05-20 design decision, pages compose UI only from `Simf*` components,
  never raw primitives: `SimfAuthLayout`, `SimfAuthCard`, `SimfBrandPanel`,
  `SimfTextField`, `SimfPasswordField`, `SimfCodeField`, `SimfButton`,
  `SimfAlert`, `SimfLink`, `SimfThemeToggle`, `SimfLanguageSwitch`,
  `SimfIcon` and the signed-in landing. The login-tier components are semantic
  HTML styled by the design tokens (decision D-021).
- **Design tokens** — `theme.tokens.css` (in `SIMF.Components`) is the single
  source of truth for every colour, font, size and space, built from
  SIMF-VID-001 section 9 and SIMF-CPD-001 section 8. It carries the brand
  tokens, the derived functional tokens, and a light and a (proposed) dark
  theme switched by a `data-theme` attribute. No SIMF stylesheet, component or
  page uses a raw colour or font name. `app.css` holds resets only;
  `simf-components.css` holds the BEM component styles.
- **`SIMF.ApiClient`** — `SimfAuthClient`, a typed client over the Login API
  that returns the `ApiResult<T>` envelope (a transport failure is mapped to a
  failed envelope, so a page branches one way only). `SimfAuthSession` holds
  the signed-in tokens for the lifetime of the Blazor circuit — on the server,
  never in the browser.
- **Pages** — each app has `/login`, the second-factor step (`/login/totp`
  for the Control Panel, `/login/verify` for the Website), `/forgot-password`,
  `/reset-password`, and a signed-in landing placeholder at `/`. The pages use
  `EditForm` with a `ValidationMessageStore`; the `Simf*` field components read
  and render their own validation message.
- **Bilingual readiness** — the components use CSS logical properties
  throughout, so the layout mirrors for Arabic (RTL) without a second layout.
  The pages ship with English text; Arabic/English resource-file localisation
  arrives with the Control Panel base shell (decision D-022).

The applications read the Login API base address from `Api:BaseUrl` in
`appsettings`. The white (negative) SIMF logo is placed on the navy brand
panel of the sign-in screens; the white card keeps the wordmark placeholder
until the standard (dark) logo is delivered, and the compass-and-anchor
background pattern is still pending (SIMF-VID-001 OI-2).

### 12.1 The public Website content

The Website's public content is the pre-built static SIMF 2026 marketing site,
imported verbatim into `SIMF.Web/wwwroot` (`index.html`, `content.js`,
`assets/`) and served at `/` by `UseDefaultFiles` + `UseStaticFiles` (decision
D-024). The Blazor authentication pages are interactive islands on the same
host at `/login`, `/login/verify`, `/forgot-password`, `/reset-password`; the
signed-in landing placeholder is at `/account`. The marketing site is not
edited and is not built from Razor — it is static content served as-is.

## 13. The Control Panel base shell

This increment builds the Control Panel's application shell — the frame every
future module page sits in — with persistent authentication and bilingual
localisation.

- **Persistent authentication** — sign-in issues a cookie (decision D-026), so
  the session survives a refresh and `[Authorize]` pages are real. A cookie is
  written only in an HTTP request context, so the interactive verification page
  stashes the token pair in `SignInTicketStore` and full-page-navigates to
  `/auth/complete`, which issues the cookie. `/auth/sign-out` (POST) ends the
  API session and clears it. `Routes.razor` uses `AuthorizeRouteView`.
- **The shell** — new `Simf*` components: `SimfAppShell` (top bar, side
  navigation, content region), `SimfNavGroup`, `SimfNavItem`, `SimfPageHeader`.
  The layout `CpShellLayout` wires them. The render mode is set globally on the
  Control Panel (Interactive Server, no prerender) — a page-level render mode
  does not reach the layout (decision D-027).
- **Navigation** — `CpNavigation` defines the nine groups and their modules
  (SIMF-CPD-001 §5.1). Permission filtering is gated on D1 and not yet applied;
  the navigation shows its full structure. Each module routes to a shared
  `ModulePlaceholder` until its feature increment is built.
- **Localisation** — English and Arabic, switched from the top bar through the
  `/culture` endpoint and a culture cookie; `RequestLocalization` selects the
  culture; strings come from `Resources/Strings.resx` / `Strings.ar.resx` via
  `IStringLocalizer<Strings>`. The shell is direction-aware — RTL for Arabic —
  through CSS logical properties; `<html lang>`/`dir` follow the culture.
  English is the default; the Control Panel sign-in pages stay English for now
  (decision D-028).
- **Pages** — the Dashboard (the Overview landing), the module placeholder, and
  a "not permitted" page, all inside the shell.

The per-feature Control Panel modules are built in their own later increments.

---

# Part II — Programmer Manual

> **Authored:** D-133 slice 6 (2026-05-28). The sections above are the
> sprint-by-sprint build log; this part is the **conceptual + operational
> reference** a new developer reads on day one.

## 14. How to read the rest of this document

If you've never opened the repo before, read §15 → §20 → §24 (this is
roughly half a day of reading). After that, dip into §16-§19 when you
touch the matching layer, §21-§23 when you ship a change.

The reference is biased toward **"how SIMF does it"** — not generic .NET
advice. Where a pattern is non-obvious (e.g. the cookie-refresh hook in
§18, the audit interceptor in §17.4, the BFF passthrough split in §19.3),
the section names the relevant decision-log entry so you can dig deeper.

## 15. Architecture overview

### 15.1 The big picture

```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│ Visitors            │     │ Administrators      │     │ Visitors (mobile)   │
│ via the Website     │     │ via the CP          │     │ via the Flutter App │
│ (Blazor SSR)        │     │ (Blazor Server)     │     │ (deferred)          │
└──────────┬──────────┘     └──────────┬──────────┘     └──────────┬──────────┘
           │ HTTPS                     │ HTTPS                     │ HTTPS
           ▼                           ▼                           ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ SIMF.Api  — FastEndpoints + ASP.NET Core + JWT bearer + rate limiting +     │
│           ApiResult<T> envelope. The single source of truth for every       │
│           business write. Talks to:                                          │
└──────────┬───────────────────────────────────────────────────────────┬───────┘
           │ EF Core 10                                                │ SignalR
           ▼                                                           ▼
┌──────────────────────┐                                  ┌──────────────────────┐
│ SQL Server           │                                  │ SIMF.RealTime hubs   │
│ [identity] schema    │                                  │ (live moderation,    │
│ [app] schema         │                                  │  attendance, etc.)   │
└──────────────────────┘                                  └──────────────────────┘
```

The CP and Website talk to the API through **BFF passthrough routes**
(`/account/api/*`) — see §19.3.

### 15.2 DDD layering

Project dependencies are **inward**. From outside-in:

```
SIMF.Api  ─┐
SIMF.ControlPanel ─┤  (host projects — own Program.cs, DI, HTTP)
SIMF.Web  ─┘
            ↓
SIMF.Infrastructure  (EF, ASP.NET Identity, email, file storage)
            ↓
SIMF.Application  (services + abstractions + use cases + validators)
            ↓
SIMF.Domain  (entities, value objects, enums, domain rules)
            ↑
SIMF.Common  (leaf — used by every project: ApiResult, ErrorCodes, options)
SIMF.Contracts  (leaf — DTOs the API + clients share)
SIMF.ApiClient  (leaf-ish — typed HttpClient + envelope unwrap)
SIMF.Components  (leaf — Blazor Simf* components + design tokens)
```

**Rules** (SIMF-SES-001 §6, enforced by review):
- Domain references **nothing** outside itself + Common.
- Application references Domain + Common + Contracts.
- Infrastructure references Application + Domain + Common.
- Host projects (Api, ControlPanel, Web) reference everything below.
- **No circular references.** `Directory.Build.props` sets
  `TreatWarningsAsErrors=true`; a circular reference fails Release build.

### 15.3 Bounded contexts today

Sprint 1 + the User Management increment carved out one bounded context:
**Identity & Access** (sign-in, registration, approval, profile,
notifications, audit). Future increments will add: **Programme** (themes,
sessions, halls, speakers, bookings — FDS-004/005), **Exhibition**
(exhibitors, booths, sponsors, venue map — FDS-006), **Engagement**
(live sessions, moderation — FDS-007), and **Content** (FAQ, media,
news — FDS-008/010). See `docs/SIMF-SAD-001` §6.

## 16. The HTTP API — FastEndpoints + ApiResult patterns

### 16.1 Endpoint shape

Every endpoint is a class deriving from `FastEndpoints.Endpoint<TRequest, TResponse>`:

```csharp
// Tests: tests/SIMF.Api.Tests/Admin/InterestsCreateEndpointTests.cs
public sealed class CreateInterestEndpoint
    : Endpoint<AdminCreateInterestRequest, ApiResult<AdminInterestSummary>>
{
    private readonly IAdminInterestService _service;

    public CreateInterestEndpoint(IAdminInterestService service) => _service = service;

    public override void Configure()
    {
        Post("/api/v1/admin/interests");
        Policies(AuthorizationPolicies.AdministratorOnly,
                 AuthorizationPolicies.RequireApprovedAccount);
        RequireRateLimiting("auth");
    }

    public override async Task HandleAsync(AdminCreateInterestRequest req, CancellationToken ct)
    {
        var created = await _service.CreateAsync(req, ct);
        await SendOkAsync(ApiResult<AdminInterestSummary>.Ok(created), ct);
    }
}
```

**Conventions** (SIMF-SES-001 §7):
- One endpoint = one class = one file. Sealed by default.
- `// Tests:` header line names the integration-test class that covers it.
- `Configure()` declares route + policies + rate limit; nothing else.
- `HandleAsync` is thin — delegates to an Application service.
- Validation lives in `Validator<TRequest>` next to the endpoint.

### 16.2 ApiResult envelope

Every response is `ApiResult<T>`:

```csharp
public sealed record ApiResult<T>(
    bool Success,
    T? Data,
    ApiError? Error);

public sealed record ApiError(
    string Code,           // from ErrorCodes (constant, never literal)
    string Message,        // English
    string MessageArabic,
    IReadOnlyDictionary<string, string[]>? Fields = null);
```

The envelope is the **contract** every API endpoint, every BFF passthrough,
and every typed client uses. A network failure becomes a failed envelope on
the client side (see `SimfAuthClient.SignInAsync` for the pattern) so the
caller branches one way only.

**Status codes** (SIMF-API-001 §6):
- 200 — success
- 4xx — client error; `Code` from `ErrorCodes`
- 500 — server error; `Code = ErrorCodes.Internal`; logged with correlation id

### 16.3 Validation

FluentValidation, registered automatically by FastEndpoints:

```csharp
public sealed class AdminCreateInterestRequestValidator
    : Validator<AdminCreateInterestRequest>
{
    public AdminCreateInterestRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);
        RuleFor(x => x.NameArabic)
            .NotEmpty()
            .MaximumLength(128);
        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}
```

**Validation alignment rule:** `MaximumLength(N)` on the validator MUST
match `HasMaxLength(N)` on the EF entity config AND `MaxLength="N"` on
the Razor SimfTextField. Mismatch is a real bug — the field accepts
input the validator silently rejects.

### 16.4 Authorization policies

Defined in `SIMF.Common.AuthorizationPolicies`:

| Policy | What it checks |
|--------|----------------|
| `AdministratorOnly` | `User.IsInRole("Administrator")` |
| `RequireApprovedAccount` | `AccountState` claim == `Approved` |
| `RequireRateLimiting("auth")` | fixed-window 5 req / 5 min / IP (FastEndpoints attribute, not a policy) |

Default for new admin endpoints: `Policies(AdministratorOnly, RequireApprovedAccount) + RequireRateLimiting("auth")`. Anonymous endpoints (sign-in, sign-up,
forgot-password, reset-password) are explicit `AllowAnonymous()` in `Configure()`.

### 16.5 Error codes

`SIMF.Common.ErrorCodes` is the **stable catalogue** of error codes:

```csharp
public static class ErrorCodes
{
    public const string Internal = "Internal";
    public const string NotFound = "NotFound";
    public const string Conflict = "Conflict";
    public const string ValidationFailed = "ValidationFailed";
    public const string EmailAlreadyExists = "EmailAlreadyExists";
    public const string InterestNameNotUnique = "InterestNameNotUnique";
    // …
}
```

**Never use a string literal for an error code.** Add a constant to
`ErrorCodes` first.

## 17. The data layer — EF Core + audit interceptor

### 17.1 Two contexts, one database

| Context | Schema | Purpose |
|---------|--------|---------|
| `SimfIdentityDbContext` | `identity` | Users, roles, permissions, refresh tokens, account codes |
| `SimfAppDbContext` | `app` | Business entities (UserProfiles, Interests, ProfileTypes, OperationLog, RowAudit) |

Same physical SQL Server database (decision C-1), separate migration
histories (`__EFMigrationsHistory_Identity` and `__EFMigrationsHistory_App`).
Both registered in `SIMF.Infrastructure.DependencyInjection.AddInfrastructure`.

### 17.2 Adding a migration

```powershell
# Identity context
dotnet ef migrations add MyMigration `
    --project src/Backend/SIMF.Infrastructure `
    --startup-project src/Backend/SIMF.Infrastructure `
    --context SimfIdentityDbContext `
    --output-dir Persistence/Migrations/Identity

# App context — same command, swap --context + --output-dir to App
```

Run on startup automatically (outside the test host) — see
`Program.cs.MigrateAsync`. **D-110 freeze:** no schema changes without
explicit owner approval.

### 17.3 Repository pattern

Interfaces in `SIMF.Application` (`IRefreshTokenRepository`,
`IAccountCodeRepository`, `IPermissionRepository`, etc.); implementations
in `SIMF.Infrastructure.Repositories`. Users + roles use ASP.NET Identity's
`UserManager` / `RoleManager` directly (no custom repository).

**Pattern:** repositories return domain entities, not DbSets. Services
own the unit-of-work (transaction + SaveChangesAsync). Repositories never
call SaveChanges.

### 17.4 Row audit interceptor (D-109)

`RowAuditingSaveChangesInterceptor` registered on both contexts:

- Fires **before** `SaveChangesAsync` commits.
- Inspects every Added / Modified / Deleted entity.
- Writes one `RowAudit` row per change with: actor (from `IActorAccessor`),
  table, primary key, operation, before/after JSON, timestamp.
- Stored in `app.RowAudit`. Queryable from `/admin/logs` viewer
  (operation-log subset).

**Implications:**
- Pure reads (`AsNoTracking`) produce **no audit row**. If a read needs to
  be audited (e.g. admin reading a PII-sensitive profile), name a
  separate audit-log entry (see D-126 for the precedent — admin profile
  read relies on the SaveChanges-side trail).
- Tracking-mode reads followed by no-change save also produce no rows
  (EF doesn't mark unchanged entities Modified).

### 17.5 Soft-delete

Pattern (SIMF-SES-001 §7):
- Entity has `bool IsActive` (default true).
- `Deactivate()` method sets `IsActive = false` + audits.
- List endpoints filter `Where(x => x.IsActive)`.
- Hard delete is reserved for compliance flows — must touch the row in
  `RowAudit` so the audit trail survives the row vanishing.

## 18. Authentication + session refresh

### 18.1 Full sign-in flow

```
1. Visitor POSTs email+password → /api/v1/auth/sign-in
2. SignInService:
     - UserManager.CheckPasswordAsync (lockout-aware)
     - rejects unless EmailConfirmed
     - issues SecondFactorToken (in-memory ticket, 5-min TTL)
     - audits Auth.SignInPasswordOk
3. UI redirects → /login/totp (CP) or /login/verify (Web)
4. Visitor POSTs 6-digit code → /api/v1/auth/totp/verify
5. SignInService:
     - validates TOTP via Otp.NET (30-s window + 1-step tolerance)
     - if PendingApproval: returns AuthRequiresApproval envelope
     - else: mints JWT access token (HS256, 30-min) + refresh token (random opaque, hashed at rest, 14-day)
     - returns AuthTokens { AccessToken, RefreshToken, AccessTokenExpiresInSeconds }
     - audits Auth.TotpVerifyOk
6. CP/Web sign-in endpoint:
     - persists tokens into cookie (D-026)
     - persists expires_at via SimfCookieRefreshHandler.StoreTokens (D-121)
     - issues cookie with 8-hour sliding lifetime
     - redirects to /account/profile (Web) or / (CP)
```

### 18.2 The D-121 refresh hook

`SimfCookieRefreshHandler` (duplicated into both `SIMF.ControlPanel` and
`SIMF.Web` per the established `SignInTicketStore` / `AuthEndpoints` split):

```csharp
options.Events.OnValidatePrincipal = SimfCookieRefreshHandler.OnValidatePrincipalAsync;
```

Runs **before every authenticated request**. Reads `access_token`,
`refresh_token`, `expires_at` from the cookie's stored tokens. When the
access token has ≤ 2 minutes left:
- Calls `SimfAuthClient.RefreshAsync(refreshToken, ct)`.
- API: `SessionService.RefreshAsync` validates + rotates (D-013) — new
  access token + new refresh token, old refresh revoked + chained.
- Persists the new tokens back into the cookie + sets `ShouldRenew = true`.

**Failure handling:**
- Refresh-token revoked / expired / rejected → `RejectPrincipal()` +
  `SignOutAsync()`. Next request lands on `/login`.
- Transient cancellation → leave principal alone. A brief API blip
  doesn't log the user out.

### 18.3 The BFF token forwarding

Every `/account/api/*` route in `SIMF.ControlPanel.Endpoints.AccountEndpoints`
(mirror in `SIMF.Web.Endpoints.AccountEndpoints`):

```csharp
account.MapPost("/api/admin/interests", async (
    AdminCreateInterestRequest req,
    ISimfAdminClient admin,
    HttpContext http,
    CancellationToken ct) =>
{
    var token = await http.GetTokenAsync("access_token");
    var envelope = await admin.CreateInterestAsync(req, token!, ct);
    return Results.Json(envelope);
}).RequireAuthorization();
```

The bearer comes from `GetTokenAsync("access_token")` — the cookie's
stored access token, which the D-121 hook keeps fresh. **No retry-on-401
plumbing** anywhere in the BFF — that's the whole point of D-121.

### 18.4 Stolen-token protection

`SessionService.RefreshAsync` (D-013): presenting an already-revoked
refresh token is treated as a stolen-token signal:
- Every session for the account is revoked.
- `Auth.RefreshToken.Reused` audit row written.
- Caller gets 401.

The user has to sign in again. The legitimate session keeps the new
chained token; the attacker's stolen token gets revoked first; the
legitimate user sees a forced re-login (acceptable cost for the
detection).

## 19. The Blazor frontend — components + render modes + BFF

### 19.1 Render modes

| App | Mode | Why |
|-----|------|-----|
| Control Panel | Interactive Server (no prerender) — set globally on the host | Heavy interactivity, no SEO need, server-only auth state |
| Website | SSR by default + Interactive Server islands for auth + profile | Marketing SEO + interactive islands for the few real flows |

Decision D-017 + D-027 set this. Don't change render mode per-page in the
CP — it doesn't reach the layout (D-027).

### 19.2 The Simf* component library

Lives in `src/Shared/SIMF.Components/`. Used by both CP and Website. Every
visible primitive is a `Simf*` component — pages never use raw `<input>`
/ `<button>` / `<table>`.

**Primitives** (most-used):

| Component | Use for |
|-----------|---------|
| `SimfAppShell` | The CP layout frame (top bar + nav rail + content) |
| `SimfNavGroup` / `SimfNavItem` | Side-nav. `BadgeLabel` slot for D-132 "Soon" tag. |
| `SimfBanner` | Page-top branded banner (mandatory per D-132 for list pages) |
| `SimfPageHeader` | Plain content-region `<header>` — for non-list pages only |
| `SimfDataGrid` | The canonical CRUD grid (D-117 + D-132 — see `docs/dev/SIMF_TABLE_PATTERN.md`) |
| `SimfDataGridColumn` | Grid column with optional Sortable + Filterable + cell template |
| `SimfModal` | Overlay dialog with `<dialog>` + Footer slot |
| `SimfTextField` | Text input + label + helper + validation message. `@bind-Value` for two-way binding OR `Value` + `ValueChanged` + `ValueExpression` for parsed-string controls (see InterestForm DisplayOrder for the second pattern) |
| `SimfPasswordField` / `SimfCodeField` / `SimfCheckbox` / `SimfTextarea` | Specialised inputs |
| `SimfButton` | Button with Loading state + Variant (primary / secondary) |
| `SimfAlert` | Toast / inline message with Variant (success / error / info / warning) |
| `SimfPill` | Status chip with Variant (on / off / admin / warning) |
| `SimfEmptyState` | `<SimfDataGrid><EmptyTemplate>` content |
| `SimfIcon` | Named SVG icon (D-117 expanded to 10+: plus, edit, trash, copy, upload, download, chevron-first/-left/-right/-last) |

### 19.3 BFF passthrough

The CP / Web servers proxy `/account/api/*` requests to the API. Lives in
`SIMF.ControlPanel.Endpoints.AccountEndpoints` (mirror in
`SIMF.Web.Endpoints.AccountEndpoints`) — **duplicated by design** because
the two host projects have separate auth schemes + separate clients.

**JS interop:** Razor pages call `simfAccount.postJson(url, body)`,
`simfAccount.getJson(url)`, `simfAccount.deleteJson(url)`,
`simfAccount.putJson(url, body)`, `simfAccount.uploadFile(url, inputId)`,
`simfAccount.downloadXlsx(url, body)`. All defined in
`SIMF.ControlPanel/wwwroot/js/simf-account.js` (CP) + Web equivalent.
They handle the bearer-pass-through transparently because the BFF reads
the cookie's stored access token.

### 19.4 Design tokens

`theme.tokens.css` is the **single source of truth** for every colour,
font, size, space, radius, shadow, focus ring. Two themes (`:root` light
and `[data-theme="dark"]`). No SIMF stylesheet, no SIMF component, no
SIMF page uses a raw hex / raw font-family / raw px. **Adding a new
token:** add to `theme.tokens.css` FIRST under both `:root` and the dark
theme, then use it.

`simf-components.css` holds the BEM rules built on top of the tokens.
**No scoped `.razor.css`** in any consumer project (decision SIMF-VID-001).

### 19.5 Localisation

`Strings.resx` (English) + `Strings.ar.resx` (Arabic) live in each host
project's `Resources/`. `IStringLocalizer<Strings>` injected as `L`. The
two resx files MUST stay in sync — D-132 audit verified 576/576 keys.
Adding a new EN key without the AR pair is a real bug (Arabic users see
the key name verbatim).

**Toggle:** the `/culture?culture=ar|en&redirectUri=...` endpoint in each
host stores a culture cookie + redirects.

**RTL:** `<html dir="rtl" lang="ar">` set by `RequestLocalization`
middleware. Components use CSS logical properties (`margin-inline-start`,
`padding-block`, etc.) so layout mirrors automatically.

## 20. Local development setup (deep dive)

### 20.1 First-time bootstrap

```powershell
# 1. Install .NET 10 SDK + SQL Server (LocalDB is fine)
# 2. Clone:
git clone <repo> D:\SIMF\System\V1.0.0
cd D:\SIMF\System\V1.0.0

# 3. Restore + build (Release for the zero-warning gate):
dotnet build SIMF.slnx -c Release

# 4. Apply migrations + seed super-admin (auto on first API run, but you
#    can do it explicitly):
dotnet ef database update --project src/Backend/SIMF.Infrastructure --context SimfIdentityDbContext
dotnet ef database update --project src/Backend/SIMF.Infrastructure --context SimfAppDbContext

# 5. Run the three apps (separate terminals — or use Start-Process for detached):
dotnet run --project src/Backend/SIMF.Api          # http://localhost:5175
dotnet run --project src/ControlPanel/SIMF.ControlPanel  # http://localhost:5158
dotnet run --project src/Website/SIMF.Web          # http://localhost:5115
```

### 20.2 Detached launch (avoid the harness killing background dotnet)

If you're running through a CLI that kills child processes:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
Start-Process -FilePath "dotnet" `
    -ArgumentList "run","-c","Release","--no-build","--no-launch-profile","--urls","http://localhost:5175" `
    -WorkingDirectory "D:\SIMF\System\V1.0.0\src\Backend\SIMF.Api" `
    -WindowStyle Hidden `
    -RedirectStandardOutput "D:\SIMF\System\V1.0.0\.run-api.log" `
    -RedirectStandardError "D:\SIMF\System\V1.0.0\.run-api.err.log"
```

(Same shape for CP on 5158 and Website on 5115.)

### 20.3 Secrets

**Never committed:**
- `appsettings.json` super-admin credentials
- `appsettings.Development.json` SMTP / JWT signing keys
- TOTP secrets

Use `dotnet user-secrets` or `set-env-*.ps1` scripts (out-of-repo). The
working tree's modifications to `appsettings*.json` and `myComment.txt`
are intentional un-committable state.

### 20.4 Port map

| Surface | URL |
|---------|-----|
| API | http://localhost:5175 |
| Control Panel | http://localhost:5158 |
| Website | http://localhost:5115 |
| API health | http://localhost:5175/health |
| CP sign-in | http://localhost:5158/login |
| Default super-admin | `superadmin@zagali-ict.com` / `Aa@123456789` |
| TOTP secret (dev) | `dbji csx7 c3mj s2qa sjcl rbcl kiqk ovr3` |

### 20.5 Database reset (development)

```powershell
dotnet ef database drop --project src/Backend/SIMF.Infrastructure --context SimfIdentityDbContext --force
dotnet ef database drop --project src/Backend/SIMF.Infrastructure --context SimfAppDbContext --force
# Restart the API → migrations + seed run automatically.
```

## 21. Testing

### 21.1 Test projects

| Project | What it covers | Notes |
|---------|----------------|-------|
| `SIMF.Domain.Tests` | Domain entity + value-object rules | Pure unit, no DI |
| `SIMF.Application.Tests` | Application services with mocked repos | Pure unit |
| `SIMF.Api.Tests` | Integration — `WebApplicationFactory<Program>` + EF in-memory or real SQL | Most coverage lives here |
| `SIMF.ApiClient.Tests` | Typed-client unwrap behaviour | Pure unit |
| `SIMF.ControlPanel.Tests` | CP navigation + page-rendering invariants | bUnit-light |

### 21.2 Conventions

- Each endpoint has a `// Tests:` header pointing at the test class
  (SIMF-SES-001 §7).
- Each backend bug fix lands with a regression test in the same commit
  (CLAUDE.md §3).
- Tests names follow `MethodUnderTest_Scenario_Expected` shape.

### 21.3 Running

```powershell
dotnet test SIMF.slnx -c Release           # all
dotnet test tests/SIMF.Api.Tests           # one project
dotnet test --filter "FullyQualifiedName~AdminInterestsCreate"  # by name
```

### 21.4 Known flake

`NotificationTests` reuse a token across long-running siblings; a full
~270-test run can exceed the 30-min JWT lifetime. Run failing
notification tests in isolation OR shorten `JwtOptions.AccessTokenMinutes`
for the test host. Tracked, not yet fixed.

## 22. Logging + debugging

### 22.1 Where logs go

- **Console** — Serilog writes to console when running interactively.
- **Files** — rolling daily under `logs/{Project}-{Date}.log`.
- **`/admin/logs` viewer** — tails the same files in the browser (D-117 §11.1).

### 22.2 Log levels

`appsettings.json` `Serilog` section. Default is Information; raise to
Debug per namespace when investigating:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "SIMF.Application.IdentityAccess.SignInService": "Debug"
    }
  }
}
```

### 22.3 Correlation ids

Every request gets an `X-Correlation-Id` header (echoed in responses).
Serilog enriches every log event with the id. To trace a single request
end-to-end, grep the log file for the correlation id.

### 22.4 Common issues

| Symptom | Likely cause |
|---------|--------------|
| 401 on every `/account/api/*` past 30 min | D-121 hook not wired — check `Program.cs.OnValidatePrincipal` |
| 500 on first request after restart | Migrations didn't apply — check API log for `Hosting failed to start` |
| Address-in-use binding error | A previous `dotnet run` is still alive — `Stop-Process -Id <pid>` |
| "InputText requires ValueExpression" | Used `Value`+`ValueChanged` on SimfTextField inside an EditContext-bound EditForm — add `ValueExpression="@(() => _field)"` (see D-132 mid-flight bug) |
| Cropper crashes on dispose | `cropper.min.js` not loaded before `cropperJsInterop.min.js` in `App.razor` (D-123) |

## 23. Deployment

### 23.1 Build

```powershell
dotnet publish src/Backend/SIMF.Api -c Release -o publish/api
dotnet publish src/ControlPanel/SIMF.ControlPanel -c Release -o publish/cp
dotnet publish src/Website/SIMF.Web -c Release -o publish/web
```

Release must pass with **0 warnings / 0 errors** — `TreatWarningsAsErrors`
is global in `Directory.Build.props`.

### 23.2 Production topology

- On-prem (Windows or Linux) behind a reverse proxy.
- API + CP + Website run as separate Kestrel processes.
- Reverse proxy (IIS / nginx) terminates TLS + forwards.
- `ReverseProxy:KnownProxies` MUST list the proxy IP — startup fails fast
  outside Development if empty (because rate-limit + audit source IP rely
  on trusted `X-Forwarded-For`).

### 23.3 Health

`GET /health` returns 200 + `{"status":"healthy"}` when the API +
database are reachable. Wire to the load balancer.

### 23.4 Rollback

- Keep the last known-good `publish/` folder.
- To roll back: stop process → swap folder → restart → verify `/health`.
- Database migrations are **forward-only**; rollback of schema requires
  a hand-written down-migration. Per D-110 freeze, schema doesn't change
  without explicit approval, so this is rare.

## 24. How to add a new feature (end-to-end checklist)

Following SIMF-SES-001 §6 + this guide's §16-§19 + CLAUDE.md §11:

1. **Read the relevant FDS** (`docs/SIMF-FDS-00X-*.md`) for the bounded
   context. Don't invent rules — they're already specified.
2. **Open a §11 pre-approval block** with the owner: state what you'll
   touch, file-by-file, with Risk tags.
3. **Add to `PAGE-INDEX.md`** as the first source-code-touching action
   (a new row, status ✅ Real or 🚧 Stub if you ship a placeholder).
4. **Domain** — entity + enums + value objects in `SIMF.Domain`. Keep
   business rules here.
5. **Application** — service + abstraction + validator in
   `SIMF.Application`. Repositories named via interfaces.
6. **Infrastructure** — repository implementation, EF configuration,
   migration (if you're allowed under D-110 freeze).
7. **API** — endpoint + validator in `SIMF.Api/Endpoints/...`. Add
   `// Tests:` header. Wire DI in `DependencyInjection.cs`.
8. **Contracts** — request + response DTOs in `SIMF.Contracts`. New
   `ErrorCodes` constant if you need one.
9. **Typed client** — method on `SimfAuthClient` / `SimfAdminClient` in
   `SIMF.ApiClient`. Returns `ApiResult<T>`.
10. **BFF passthrough** — route in `SIMF.ControlPanel.Endpoints.AccountEndpoints`
    AND `SIMF.Web.Endpoints.AccountEndpoints` (where applicable).
11. **CP / Web page** — Razor page under `Components/Pages/...`. Add
    `@page`, `@layout`, `@attribute [Authorize(...)]`. Use the
    canonical CRUD pattern (`docs/dev/SIMF_TABLE_PATTERN.md`) if it's a
    list page.
12. **Resx** — EN + AR keys in both `Strings.resx` and `Strings.ar.resx`.
    No hard-coded English in Razor.
13. **CpNavigation** — new `Module.X` entry if it shows in the nav.
    `IsStub: true` until the page is real.
14. **Tests** — at least one Api integration test + targeted unit tests
    for domain rules + at least one CP rendering test for non-trivial UI.
15. **Per-page doc** — copy `docs/pages/_TEMPLATE.md` to
    `docs/pages/{cp,web}/{slug}.md` and fill every section.
16. **Manual chapter** — add a chapter to `docs/manuals/Admin-Manual.md`
    (or User-Manual.md for Web pages) — most-common-tasks, screenshots,
    troubleshooting.
17. **E2E test entry** — `docs/tests/e2e/{slug}.md` Gherkin-style
    scenarios.
18. **UCS entry** — add the new use case(s) to `SIMF-UCS-001`.
19. **Decisions log** — D-### entry capturing what + why.
20. **Build + browser smoke** — Release 0/0, then verify in Chrome
    (screenshots into `docs/screenshots/`).
21. **Commit** — single coherent change. Don't bundle scope (CLAUDE.md
    §17).
22. **Stop and confirm** before pushing — `git push` is owner-approved
    per CLAUDE.md §1.8.

If any of those steps don't apply (e.g. read-only feature → no validator),
say so explicitly in the §11 block. Skipping silently is the bug.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 6).

