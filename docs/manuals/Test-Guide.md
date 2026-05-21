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
database, with a `FakeEmailSender` and a controllable `FakeTimeProvider` (so
time-dependent paths such as code expiry can be tested). Test parallelism is
disabled — the factory uses process-wide environment variables and a shared
LocalDB instance.

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
| `VerificationCodeGeneratorTests` | Unit (`SIMF.Application.Tests`) | The code is always six digits and well distributed |
| `RegistrationEndpointsTests` | Integration | sign-up 201 / duplicate 409 / weak or mismatched password 400; verify-email 200 / wrong code 400 / unknown email 404 / expired code / five-attempt lockout / already-verified rejected; resend-code invalidates the old code, issues a usable new one, 404 for unknown email |
| `IdentitySeederTests` | Integration | The super-admin is seeded with its TOTP secret; seeding is idempotent |

## 6. Increment 4a — audit log and rate limiting

| Test | Type | Verifies |
|------|------|----------|
| `AuditLogTests` | Integration | Sign-up writes a success operation-log entry; a duplicate writes a failure entry |
| `RateLimitTests` | Integration | The auth endpoints return 429 once the per-IP limit is exceeded (via `RateLimitedApiFactory`) |
| `RegistrationEndpointsTests` (added) | Integration | resend-code returns 429 once the per-account cap is reached |

`SimfApiFactory` now applies the EF migrations (rather than `EnsureCreated`), so
the migrations themselves are exercised by every integration test.

## 7. Increment 4b — sign-in and the second factors

| Test | Type | Verifies |
|------|------|----------|
| `SignInTests` | Integration | sign-in 401 (unknown email / wrong password) and 403 before email verification; a visitor completes sign-in with the emailed code; an administrator completes it with a TOTP code; verify-otp and verify-totp reject a wrong code |
