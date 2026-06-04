# SIMF Test Guide

| Field | Value |
|-------|-------|
| Document | SIMF Test Guide |
| Status | Living document — extended each increment |
| Last updated | 2026-05-22 (Sprint 1, increment 5) |
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
| `SignInTests` | Integration | sign-in 401 (unknown email / wrong password) and 403 before email verification; a visitor completes sign-in with the emailed code; an administrator completes it with a TOTP code; verify-otp and verify-totp reject a wrong code; account lockout, ticket expiry, the attempt cap, TOTP replay, the sign-in audit events |

## 8. Increment 4c — refresh, sign-out, the authentication middleware

| Test | Type | Verifies |
|------|------|----------|
| `SessionTests` | Integration | refresh rotates the token; an unknown token is rejected; reusing a rotated token revokes the whole family; sign-out needs a token, succeeds, revokes the refresh token, and an access token is rejected after sign-out |
| `JwtMiddlewareTests` | Integration | the bearer middleware rejects a forged, wrong-issuer, wrong-audience, expired or malformed token |
| `RefreshExpiryTests` | Integration | an expired refresh token is rejected |

## 9. Increment 5 — forgot, reset and change password

| Test | Type | Verifies |
|------|------|----------|
| `PasswordTests` | Integration | forgot-password gives the same response for a known and an unknown account; reset-password with a valid code sets the new password and the old one stops working; a wrong or unknown reset code is rejected; change-password requires authentication, succeeds with the correct current password and invalidates the old token, and rejects a wrong current password |

---

# Part II — Testing across all layers (D-133 slice 7)

The sprint-by-sprint sections above are the historical record. This part
is the **operational reference** — how SIMF tests are organised today,
how to run the right subset for what you're working on, and how E2E
scenarios fit alongside the unit + integration suites.

## 10. The four test layers

| Layer | Where | When to add a test |
|-------|-------|--------------------|
| **Domain unit tests** | `tests/SIMF.Domain.Tests` | Pure entity / value-object rules. No DB, no DI. |
| **Application unit tests** | `tests/SIMF.Application.Tests` | Service-level logic with mocked repositories. |
| **API integration tests** | `tests/SIMF.Api.Tests` | One per endpoint + its policy + its validator. Hosts the API via `SimfApiFactory` against a real SQL Server LocalDB. **Most coverage lives here.** |
| **E2E scenario tests** | `docs/tests/e2e/` (catalogue) + future `tests/SIMF.E2E.Tests/` (runner) | One per visible workflow. Browser-driven (Chrome DevTools MCP today, Playwright after adoption). |

A typical feature ships with tests at 2–3 layers — Domain rule + API
integration + at least one E2E scenario for the happy path.

## 11. The E2E catalogue

> **Source of truth:** [`docs/tests/e2e/README.md`](../tests/e2e/README.md)
> + per-page Gherkin files under `docs/tests/e2e/{cp|web|mobile}-{slug}.md`.

Every ✅ Real page in [`docs/pages/PAGE-INDEX.md`](../pages/PAGE-INDEX.md)
maps to at least one P0 E2E scenario. Reference catalogue files authored
in slice 7:

- [`cp-admin-interests.md`](../tests/e2e/cp-admin-interests.md) —
  full CRUD round-trip + empty + auth + validation + conflict + server
  500 + RTL (7 scenarios).
- [`cp-auth-flow.md`](../tests/e2e/cp-auth-flow.md) —
  sign-in golden + wrong password + lockout + TOTP retry + recovery
  code + pending/rejected redirect + forgot-password + D-121 refresh +
  RTL (10 scenarios).

The remaining 29 per-page catalogue files are scoped (Coverage matrix in
the README) and will land slice-by-slice as the matching code-level
tests are written.

## 12. Running each layer

### 12.1 Unit + integration (xUnit)

```powershell
# All
dotnet test SIMF.slnx -c Release

# One project
dotnet test tests/SIMF.Api.Tests

# By name
dotnet test --filter "FullyQualifiedName~AdminInterestsCreate"

# By category (skip the slow ones)
dotnet test --filter "Category!=Slow"
```

### 12.2 E2E (browser today)

Until Playwright is adopted, E2E scenarios are **smoked manually** via
Chrome DevTools MCP using the canonical PowerShell driver. The shape:

```powershell
# 1. Spin up API + CP detached (so the harness doesn't kill them)
$env:ASPNETCORE_ENVIRONMENT = "Development"
Start-Process dotnet -ArgumentList "run","-c","Release","--no-build","--no-launch-profile","--urls","http://localhost:5175" `
    -WorkingDirectory "src/Backend/SIMF.Api" -WindowStyle Hidden `
    -RedirectStandardOutput ".run-api.log" -RedirectStandardError ".run-api.err.log"
Start-Process dotnet -ArgumentList "run","-c","Release","--no-build","--no-launch-profile","--urls","http://localhost:5158" `
    -WorkingDirectory "src/ControlPanel/SIMF.ControlPanel" -WindowStyle Hidden `
    -RedirectStandardOutput ".run-cp.log" -RedirectStandardError ".run-cp.err.log"

# 2. Drive the browser via MCP
#    - navigate /login → fill email + password → click Sign in
#    - generate TOTP via Get-Totp 'dbji csx7 c3mj s2qa sjcl rbcl kiqk ovr3'
#    - fill code → click Verify
#    - walk the scenario steps, screenshot each state into docs/screenshots/

# 3. Tear down
Get-NetTCPConnection -LocalPort 5175,5158 -State Listen |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

The `Get-Totp` PowerShell function lives at the head of every
chrome-devtools-mcp session in this repo — paste it from the Developer
Guide §20.4 if you need it.

When Playwright is adopted: the existing Gherkin scenarios under
`docs/tests/e2e/` copy 1-to-1 into `.feature` files; the step
definitions wrap the same API calls + UI assertions.

## 13. Coverage gates

- Every endpoint has a `// Tests:` header pointing at the test class
  (SIMF-SES-001 §7).
- Every bug fix lands with a regression test in the same commit
  (CLAUDE.md §3).
- Every ✅ Real page in `PAGE-INDEX.md` has at least one P0 E2E scenario
  (catalogue or implemented).
- Release build is **0 warnings / 0 errors**; `TreatWarningsAsErrors`
  is set globally.
- A PR that changes a `@page` route / a public API contract / a resx
  key without also updating the per-page doc + catalogue + manual is
  incomplete.

## 14. Known flakes

- **`NotificationTests`** reuse a token across long-running siblings;
  the full ~270-test run can exceed the 30-min JWT lifetime
  (`JwtOptions.AccessTokenMinutes`). Failing tests pass in isolation.
  Fix is to shorten the access-token lifetime for the test host OR
  advance `FakeTimeProvider` mid-fixture. Tracked, not yet fixed.
- **Cropper-related tests** are not yet covered by `bUnit` — the
  component requires DI + JS interop that `bUnit` doesn't host well.
  Manual smoke is canonical for cropper UX (D-122 / D-123).

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 7).

