# SIMF Developer Guide

| Field | Value |
|-------|-------|
| Document | SIMF Developer Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-21 (Sprint 1, increment 3) |
| Related | SIMF-SES-001, SIMF-SAD-001, SIMF-Sprint1-Login-API-Plan |

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
      Shared/
        SIMF.Contracts         Request and response DTOs
        SIMF.Common            ApiResult envelope, error model, shared constants
    tests/
      SIMF.Domain.Tests
      SIMF.Application.Tests
      SIMF.Api.Tests

Project dependencies follow the DDD layering of SIMF-SAD-001 section 6 —
dependencies point inward: Application depends on Domain; Infrastructure on
Application and Domain; Api on Application, Infrastructure, RealTime, Contracts
and Common.

## 3. Build, run and test

| Action | Command |
|--------|---------|
| Build | `dotnet build SIMF.slnx -c Release` |
| Run the API | `dotnet run --project src/Backend/SIMF.Api` |
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
