# SIMF Developer Guide

| Field | Value |
|-------|-------|
| Document | SIMF Developer Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-21 (Sprint 1, increment 1) |
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
