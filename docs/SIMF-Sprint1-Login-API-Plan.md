# Sprint 1 — Login API: Implementation Plan (for discussion)

| Field | Value |
|-------|-------|
| Document | SIMF Sprint 1 — Login API Implementation Plan |
| Version | 2 (revised after the two architecture reviews) |
| Status | **Working document — for discussion. NOT approved. No code written.** |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Date | 2026-05-21 |
| Related | SIMF-FDS-001, SIMF-API-001, SIMF-SAD-001, SIMF-DAT-001, SIMF-SES-001, SIMF-RPM-001, SIMF-OPS-001 |

This is version 2 of the plan. It folds in two architecture reviews — an
authentication design review and a 150,000-user scalability review — both
approved by the client to act on. Section 12 lists the amendments these reviews
force on the approved baseline documents.

---

## 1. Confirmed decisions

| # | Decision |
|---|----------|
| D1 | Use **Microsoft ASP.NET Core Identity** — `PasswordHasher` for hashing, Identity's TOTP provider for admin 2FA, and **Identity's lockout** (see §5.4). Follow Microsoft's published guidance (§4). |
| D2 | Development database server: **MSSQLSERVER / SQLEXPRESS**; connection configuration follows the IBS pattern (open point O-1). |
| D3 | The email-code sending and configuration are **copied from IBS v10**, and reviewed against the SIMF security baseline before they are trusted (open point O-1). |
| D4 | Bootstrap administrator **`superadmin@zagali-ict.com`**; every seeded user gets a temporary password and is forced to change it on first sign-in; an admin-added user is auto-approved, a self-registered user needs admin approval. |
| D6 | Build the whole authentication feature, including forgot-password and change-password. **Admin / Control Panel users sign in with TOTP; visitors sign in with an email OTP.** |
| **C-1** | **One physical database, two `DbContext`s** (`SimfIdentityDbContext`, `SimfAppDbContext`) with **separate migration histories** — the migration isolation that was wanted, without losing atomic transactions or cross-table foreign keys. (Supersedes the v1 "two physical databases" idea.) |

## 2. The reviews applied

Two reviews were run and approved to act on:

- **Authentication design review** — found 3 critical, 8 important, 8 minor
  items. All are folded into this plan (§5–§9) and into the document amendments
  (§12).
- **Scalability review (150,000 users)** — found the design is sized for the
  average case and will not safely carry the event-day peaks. Its fixes are
  mostly system-wide (SignalR backplane, topology, GPS-presence, caching) and
  go into SAD-001 / OPS-001 / DAT-001 / the event-day FDS specs (§12). The parts
  that touch the Login API directly are in §9.

## 3. Database design

One physical SQL Server 2022 database. Two EF Core contexts:

- **`SimfIdentityDbContext`** — users, roles, permissions, refresh tokens,
  account codes, TOTP secrets.
- **`SimfAppDbContext`** — all other SIMF data.

Each context has its **own migration history table** (`__EFMigrationsHistory`
named per context) so migrations are generated and applied independently. Both
contexts target the same database, so a unit of work that spans identity and
application data is still one transaction and foreign keys still hold. This
**amends SIMF-DAT-001 §3/§7** (which said "one database" with the implication
of one context) — handled through the change process (§12).

## 4. Microsoft best practice (D1)

From the ASP.NET Core 10 documentation:
- JWT bearer — a short-lived **access token** (the `Authorization` header) plus
  a longer-lived **refresh token**; the API only authorises (401/403), never
  redirects for a token.
- Passwords — use `PasswordHasher<TUser>`; `IterationCount` is tunable; do not
  hand-roll PBKDF2.
- Identity — password, **lockout** and token-provider options are set centrally
  via `IdentityOptions`.

Sources are listed at the end.

## 5. Authentication design

### 5.1 The two sign-in paths (D6)
- **Admin / Control Panel users** — email + password → a **TOTP** code from an
  authenticator app (Microsoft Identity two-factor).
- **Visitors** — email + password → an **email OTP** code.

Both paths: the password step issues no tokens; it returns a short-lived,
single-use **second-factor token** (`mfaToken` for TOTP, `otpToken` for email
OTP — §5.5). Tokens are issued only after the second factor succeeds.

> This and the visitor second factor **amend SIMF-API-001 §12 and SIMF-FDS-001
> §5.6**, which had no visitor second factor. The new endpoint contract is in
> §6. Open point O-2 still asks whether the visitor flow keeps the password or
> is OTP-only — confirm before increment 4.

### 5.2 Account states and approval (D4)
States per SIMF-RPM-001 §6. A self-registered visitor lands in
`PendingApproval` and needs an admin to approve them; an admin-created user is
`Approved` at once. A seeded user (including the superadmin) is in a
**password-change-required** state — see §5.6.

### 5.3 Refresh tokens
Random, opaque, **stored only as a hash**; rotated on every use with a
`RotatedFromId` chain; a token presented after rotation is treated as reuse —
rejected and logged. A successful password reset revokes every refresh token.

### 5.4 Account lockout and brute-force control (review C-2)
- **ASP.NET Core Identity lockout is enabled** — a configured failed-attempt
  threshold and lockout window on the password step.
- Each verification / reset / TOTP / email-OTP code carries a **per-code
  attempt counter** — the code is invalidated after a small number of failed
  attempts, independent of its time expiry.
- A new error `AUTH_ACCOUNT_LOCKED` (HTTP 423/429) is added to SIMF-API-001.
- Rate limiting stays, but is **tuned for shared NAT** — venue Wi-Fi puts many
  legitimate users behind one IP, so per-IP limits are generous and the tighter
  limits are per-account (review M-3).

### 5.5 The second-factor tokens (review I-5)
`mfaToken` (admin TOTP) and `otpToken` (visitor email OTP): short lifetime
(2–5 minutes), **single-use**, **stored hashed**, invalidated after the per-code
attempt cap, and bound to the originating sign-in. A failed second factor does
not silently reissue the token.

### 5.6 Forced password change (review I-7)
A seeded user (the superadmin, any admin-created user given a temporary
password) is flagged **password-change-required**. Until they change it, the
only action allowed is `change-password`. A new error
`AUTH_PASSWORD_CHANGE_REQUIRED` signals this; `POST /auth/change-password` is a
specified endpoint (§6).

### 5.7 The superadmin TOTP bootstrap (review I-8)
The superadmin is an admin user, so sign-in needs TOTP — but TOTP enrolment was
deferred. Resolution: **the superadmin's TOTP secret is seeded with the
account**, and the secret / QR is delivered to the operator out-of-band through
the `set-env-*` script. The system is therefore administrable from first run.
A general first-sign-in TOTP enrolment for other internal users is specified
with SIMF-FDS-002.

### 5.8 Token revocation window (review I-2)
An access token is valid for 30 minutes; disabling an account or revoking a
role is otherwise not felt until it expires. Resolution: the access token
carries a **per-user security stamp**; on **admin/Control-Panel-sensitive
endpoints** the stamp is checked server-side, so disabling an admin or revoking
a Control Panel role takes effect immediately. The Administrator's permissions
are carried as a **role reference plus a server-side lookup**, not the full
permission catalogue, to keep the token small.

### 5.9 Sessions (review I-6)
Concurrent sessions are allowed (web, app, Control Panel). Sign-out is
per-device. An admin can **force-sign-out a user** (revoke all their refresh
tokens) — specified in SIMF-FDS-001.

## 6. The endpoints

`POST /api/v1/auth/` — `sign-up`, `verify-email`, `resend-code`, `sign-in`,
`verify-totp` (admin), **`verify-otp` (visitor — new)**, `refresh`, `sign-out`,
`forgot-password`, `reset-password`, **`change-password` (new)**.

`verify-otp` and `change-password` are added to SIMF-API-001 §12 with full
request/response/error contracts (§12 amendments).

## 7. Middleware pipeline (reviews I-3, I-4)

Order: request logging → **localisation** → **error handling** →
standard-headers → **rate limiting (IP-partitioned)** → **authentication (JWT)**
→ **rate limiting (per-user)** → authorisation → endpoints.

- **Localisation early**, so even an early failure is localised (`Accept-Language`).
- **Error-handling middleware** maps `DataValidationException` → 400
  `VALIDATION_FAILED`, domain exceptions → their codes, unhandled → 500; always
  an `ApiResult<T>`; logged via Serilog.
- **Rate limiting is split** — an IP-partitioned stage before authentication and
  a per-user stage after (per-user limits need the identity).
- **Authentication** — JWT bearer; validates issuer/audience/lifetime/key;
  builds claims.
- **Anti-forgery** — SIMF is a **bearer-token API**, not cookie-auth, so it is
  not CSRF-exposed at the API tier; the blanket `X-Anti-Forgery` requirement in
  SIMF-API-001 §5 is **scoped to the Blazor cookie surfaces only** (§12
  amendment). The pipeline carries no anti-forgery stage for the API.

## 8. Configuration

- **JWT settings** — issuer, audience, the 30-minute access / 30-day refresh
  lifetimes in `appsettings.json`; the **signing key** in the `set-env-*`
  scripts (SIMF-SES-001 §4.4). Never committed.
- **Connection string** — one database; the configuration pattern follows IBS
  (O-1).
- **Email** — sending code and config copied from IBS v10, reviewed first (O-1).
- **Redis** — the system adopts Redis for the SignalR backplane and caching
  (scalability review). The Login API itself does not need Redis for its
  endpoints, but its **read-mostly configuration** (`ContentBlock`, `Category`,
  roles/permissions) is cached (§9).

## 9. Scale and resilience relevant to Sprint 1

The system-wide scalability fixes live in SAD-001 / OPS-001 / DAT-001 (§12).
What the Login API increments carry now, so they are not retrofitted later:

- **Stateless and multi-node ready** — no in-process session state; the API
  runs behind the reverse proxy as multiple instances. Confirmed per endpoint.
- **Async, queued email** — sign-up and code-resend **do not block** on the
  email gateway; the email is handed to a background sender. A slow gateway
  during the registration surge must not exhaust API threads.
- **External-call resilience** — the email adapter has a timeout, a bounded
  retry with backoff, and a circuit breaker (`Microsoft.Extensions.Http`
  resilience).
- **Connection pooling** — explicit `Max Pool Size` per connection, async EF
  Core throughout, a command timeout, and `EnableRetryOnFailure` for transient
  SQL errors.
- **Caching** — the read-mostly catalogues and configuration are cached
  (in-memory per node, Redis as the shared store) with invalidation on a
  Control-Panel edit.
- **Readiness `/health`** — checks the database, migrations and Redis, so the
  load balancer pulls an unhealthy node.
- The **registration-surge load test** (review C-4 #1) is part of the Sprint 1
  exit criteria, not a later afterthought.

## 10. Delivery discipline (client instruction)

For every new function, class or page: **unit tests + integration tests**, a
**project-memory update**, and the documentation set extended per page/class —
**User Manual, Developer Guide, Test Guide, E2E test documentation**. A
documentation home is set up under `docs/manuals/`. (Review M-7: confirm "per
class" is intended, or scope the manuals per feature.)

## 11. Proposed delivery — increments

1. Solution scaffold + `ApiResult<T>` + the §7 middleware + readiness `/health`.
2. The IdentityAccess domain + the two `DbContext`s (one database) + the first
   migration of each + repositories + connection pooling.
3. Sign-up + verify-email + resend-code + async email + the approval rule + the
   seeded `superadmin@zagali-ict.com` (with its TOTP secret) + the
   registration-surge load test.
4. Sign-in (both paths) + the second factors (TOTP / email OTP) + lockout +
   refresh rotation + JWT auth + the security-stamp revocation check + sessions.
5. Forgot-password + reset-password + change-password + the forced-change state.

Each increment: tests, the manual/guide/E2E docs, a memory update, a clean
Release build, its own commit.

## 12. Amendments to the approved baseline (the change process)

These reviews change Approved controlled documents; each is revised through the
SIMF-DMP-001 change process and its version is bumped:

| Document | Amendment |
|----------|-----------|
| SIMF-API-001 | Add `verify-otp` and `change-password` endpoint contracts; add `AUTH_ACCOUNT_LOCKED`, `AUTH_OTP_INVALID`, `AUTH_OTP_EXPIRED`, `AUTH_PASSWORD_CHANGE_REQUIRED`; scope `X-Anti-Forgery` to the Blazor cookie surfaces only |
| SIMF-FDS-001 | Lockout + per-code attempt cap; `mfaToken`/`otpToken` lifetime, single-use, hashing; the visitor second factor; sessions + admin force-sign-out; the superadmin TOTP bootstrap; the forced-password-change state |
| SIMF-SAD-001 | Multi-instance API tier + HA SQL Server topology (§10); the Redis SignalR backplane (§6.4); a caching layer (§11); resilience patterns (§9); the token-revocation window (§8) |
| SIMF-DAT-001 | One database, two `DbContext`s, separate migration histories (§3/§7); `GpsPresence` as batched append-only telemetry with a retention rule (§5.11); the peak-load indexes (§8) |
| SIMF-OPS-001 | Rewrite the §11 load test to peak-shaped targets; the multi-node + HA topology (§3); connection-pool sizing (§6); a real readiness `/health` (§9) |
| SIMF-FDS-003/005/007/009/011 | GPS interval + batched writes; graceful seat contention + held-seat expiry; SignalR backplane, group fan-out, comment batching; async queued notifications + retry policy; aggregated live counts |

## 13. Open points — still to settle

| ID | Open point |
|----|-----------|
| O-1 | **Where is the IBS v10 system?** D2/D3 depend on it. Candidates on `D:\`: `InsuranceBrokerCompany`, `ERP_V10`, `Online_ERP_System`, `ibs-probe`. Confirm the path. |
| O-2 | **Visitor login** — email + password + email OTP, or passwordless (email + OTP only)? |
| O-3 | **Infrastructure** — the scalability fixes add **Redis**, a **multi-instance API tier**, and **HA SQL Server**. These have hosting cost and capacity implications the owner must confirm with STC (folds into OPS-001 OI-1). The SQL Server **edition** (Standard vs Enterprise) must be chosen with HA and the SQL Express 10 GB cap in mind. |
| O-4 | Git branch — a `feature/login-api` branch; the documentation baseline merged to `main` via a pull request. |

## 14. Next step

Settle O-1 and O-2 (and O-3 for the owner). Then increment 1 begins. Increment 1
(the scaffold) needs nothing from you and can start on approval; increments 2–5
need O-1.

---

## Sources (D1 research)

- [Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Overview of ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0)
- [Configure ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [PasswordHasher&lt;TUser&gt; Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1?view=aspnetcore-10.0)
- [Hash passwords in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-8.0)

---

*End of working document v2 — for discussion.*
