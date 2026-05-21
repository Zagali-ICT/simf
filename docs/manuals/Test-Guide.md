# SIMF Test Guide

| Field | Value |
|-------|-------|
| Document | SIMF Test Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-21 (Sprint 1, increment 3) |
| Related | SIMF-TST-001, SIMF-SES-001 |

How SIMF tests are organised and run. It grows one section per increment.

## 1. Test projects

| Project | Covers |
|---------|--------|
| `SIMF.Domain.Tests` | Domain entities and rules |
| `SIMF.Application.Tests` | Use-case handlers and validators |
| `SIMF.Api.Tests` | API endpoints — unit and integration |

Framework: xUnit. API integration tests host the API through `SimfApiFactory`
(`Microsoft.AspNetCore.Mvc.Testing`) against a throwaway SQL Server LocalDB
database and a `FakeEmailSender`. Test parallelism is disabled — the factory
uses process-wide environment variables and a shared LocalDB instance.

## 2. Running the tests

    dotnet test SIMF.slnx -c Release

Every behaviour change ships with tests (SIMF-SES-001 section 11); a fixed bug
ships with a regression test.

## 3. Increment 1 — the scaffold

| Test | Type | Verifies |
|------|------|----------|
| `ApiResultTests` | Unit | `ApiResult<T>.Ok` and `.Fail` build the correct envelope |
| `HealthEndpointTests` | Integration | `GET /health` returns 200 and `Healthy` |

`SIMF.Domain.Tests` and `SIMF.Application.Tests` hold no tests yet — the Domain
and Application layers gain code from increment 2.

## 4. Increment 2 — the data layer

| Test | Type | Verifies |
|------|------|----------|
| `RefreshTokenTests` | Unit | `RefreshToken.IsActive` for live, revoked and expired tokens |

## 5. Increment 3 — account creation

| Test | Type | Verifies |
|------|------|----------|
| `VerificationCodeGeneratorTests` | Unit | The verification code is six digits |
| `RegistrationEndpointsTests` | Integration | sign-up 201 / duplicate 409 / weak password 400; verify-email 200 / wrong code 400 / unknown email 404; resend-code invalidates the previous code |
| `IdentitySeederTests` | Integration | The super-admin is seeded; seeding is idempotent |
