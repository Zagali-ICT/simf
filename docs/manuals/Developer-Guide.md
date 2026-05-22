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
