# SIMF — Sprint 1 (Login API) Completion Report

**Sprint:** 1 — Login API + frontend login + visitor lifecycle + hardening
**Branch:** `feature/login-api`
**Status:** Closed (local; not yet pushed)
**Date closed:** 2026-05-25

This document closes Sprint 1 per the programme plan. It enumerates what
shipped, what was accepted as risk, and what was explicitly deferred.

---

## 1. Scope shipped

### 1.1 Backend (`src/Backend/`)

- **Login API** (`SIMF.Api` + `SIMF.Application` + `SIMF.Infrastructure` +
  `SIMF.Domain`) — sign-up, email verification, sign-in, JWT access tokens
  with HS256 signature, refresh-token rotation with reuse detection,
  sign-out (security-stamp roll + refresh-token revoke).
- **Two-factor authentication** — emailed-OTP for visitors, TOTP for
  administrators, recovery codes for TOTP-locked accounts, audience gate
  (CP / Web / App) keyed on `UserType`.
- **Approval workflow** — admin-driven approve/reject for three
  UserTypes (Admin / Other / Visitor); rejected accounts carry a
  bilingual reason persisted on the user row + sent by email + raised as
  an in-app notification.
- **User self-service** — profile (`UserProfile` entity, P8 rename from
  `VisitorProfile`), encrypted ID-document storage (AES-256-GCM,
  filesystem-backed), Interests M-to-M lookup (P9), avatar
  upload/delete.
- **Notifications foundation** — `Notification` entity, dispatcher,
  in-app fetch endpoints, email enqueue side-effect, bell component with
  60s polling and now-localised + a11y-hardened dropdown.
- **Operation log** — every credential-flow and admin event audited;
  log viewer page in the CP.
- **Bilingual everything** — every user-facing string and every
  `ApiError` carries English + Arabic (D-030).

### 1.2 Frontend

- **Control Panel** (`SIMF.ControlPanel`, Blazor Server interactive) —
  sign-in (password + OTP / TOTP / recovery code), forgot/reset
  password, admin user management split by UserType (P7e), profile
  edit, notifications list, log viewer, language switch, theme toggle,
  state-banner pages for `PendingApproval` / `Rejected` (P11), skip-to-main
  content (H9).
- **Website** (`SIMF.Web`, Blazor SSR + interactive auth islands) —
  sign-in, sign-up, verify-email, forgot/reset password, account/profile,
  notifications, state-banner pages, culture-aware `<html lang dir>` (H6).
- **Shared component library** (`SIMF.Components`) — semantic-HTML
  `Simf*` primitives, theme tokens, RTL support, notification bell,
  file upload with picked-file live region (H14).
- **Typed API client** (`SIMF.ApiClient`) — `SimfAuthClient`,
  `SimfAuthSession`.

### 1.3 Decisions log

`docs/decisions/DECISIONS_LOG.md` runs **D-001 → D-072**. Sprint 1
material:
- D-001 → D-048 cover the Login API base.
- D-049 → D-055 cover P-series (user-management module, three-UserType
  split, UserProfile rename, Interests M-to-M, JWT account_state,
  state-banner pages, notifications, lifecycle wire-up).
- **D-056 → D-072 cover the hardening series (H1 → H17)** addressed by
  this sprint:

| ID | Hardening item | Closes |
|----|----------------|--------|
| D-056 | H1 — `RequireApprovedAccount` policy sweep on all admin endpoints | Security SEV-1.1 (pre-rev) |
| D-057 | H2 — Transactional auto-transition + refresh-token revoke | Security SEV-1.2 (pre-rev) |
| D-058 | H3 — Notification bell + state-banner a11y | A11y SEV-1.3 (pre-rev) |
| D-059 | H4 — `PasswordChangeRequired` enforced at sign-in | Security SEV-1.2 |
| D-060 | H5 — JWT `security_stamp` claim required, constant-time compare | Security SEV-1.3 |
| D-061 | H6 — Website `App.razor` culture-aware `<html lang dir>` | A11y SEV-1.1 |
| D-062 | H7 — Per-email rate-limit partition | Security SEV-1.4 |
| D-063 | H8 — `ChangePasswordRequest.CurrentPassword` length cap | Code SEV-1.2 |
| D-064 | H9 — Skip-to-main-content link (CP shell) | A11y SEV-1.4 |
| D-065 | H10 — Email-enqueue failures audited as distinct event | Code SEV-1.1 |
| D-066 | H11 — `PasswordChange.Failed` audit splits wrong-current vs policy | Code SEV-1.3 |
| D-067 | H12 — Password-reset success test asserts the credential step | Code SEV-1.4 |
| D-068 | H13 — File-input pickers have programmatic accessible names | A11y SEV-1.2 |
| D-069 | H14 — `SimfFileUpload` announces picked filename | A11y SEV-1.3 |
| D-070 | H15 — H1 gate parameterised across every admin endpoint class | Reality F4 |
| D-071 | H16 — H2 transaction rollback regression-tested | Reality F5 |
| D-072 | H17 — A11y-markup regression net for H3 / H6 / H9 / H13 | Reality F6 |

---

## 2. Test totals

| Suite | Count |
|-------|-------|
| SIMF.Api.Tests | 217 |
| SIMF.ControlPanel.Tests | 32 |
| SIMF.ApiClient.Tests | 13 |
| SIMF.Domain.Tests | 3 |
| SIMF.Application.Tests | 2 |
| **Total** | **267 / 267 passing** |

Build at HEAD: `dotnet build -c Debug SIMF.slnx` — **0 warnings, 0 errors**.
A Release-config build (per CLAUDE.md §9) should be run as a sign-off
step before deploy — that is recorded as outstanding (item 3.6).

---

## 3. Outstanding items the sprint did NOT close

These items remain after Sprint 1 closes. Each is recorded with a
status: **DEFER** (re-scoped to a later sprint, no action needed
locally), **DECISION** (waiting on the owner's call), or **HARDENING**
(real defect to address before deploy).

### 3.1 Committed secrets in `appsettings*.json` — DECISION

`src/Backend/SIMF.Api/appsettings.json` carries the super-admin
temp password (`Aa@123456789`), the TOTP seed, and the
`Jwt:SigningKey`. `src/Backend/SIMF.Api/appsettings.Development.json`
carries `Storage:UserIdDocumentEncryptionKey`. Anyone who clones the
repo holds the same keys. The pre-deploy choice is between (a)
rotate-everything-then-scrub-history, (b) `.gitignore` + accept that
the history holds them, or (c) deploy with new keys via env vars and
accept that the committed values become a stale local-dev convenience.
This is operational, not code-fixable in-sprint.

### 3.2 Architectural refactor — DEFER

The post-H1/H2/H3 5-agent architecture review flagged six SEV-1s:

- Domain depends on ASP.NET Core Identity (framework leak through the
  pure-model layer).
- `IAdminAccountService` is a 17-method god service across four
  concerns.
- Four bounded contexts share `SimfIdentityDbContext`.
- Application bypasses repository abstractions for `UserManager` /
  `RoleManager` directly.
- `Storage:*` config keys read with raw `IConfiguration[...]` in three
  places (no typed options class).
- `*Service` placement inconsistency (half in Application, half in
  Infrastructure).

These are not in-sprint fixes — each is a planned refactor that touches
~10+ files. Recorded for a dedicated refactor sprint before
User Management module build-out lands on top.

### 3.3 Website skip-link — DEFER

H9 added the skip-link to the Control Panel shell only. The Website
has no comparable navigation block today, so the skip-link is lower
priority. Add when the public-site nav is finalised.

### 3.4 Full bUnit harness — DEFER

H17 ships markup-source assertions, not runtime tests. A bUnit harness
with mocked `IJSRuntime` / `NavigationManager` / `AuthenticationStateProvider`
would prove the runtime behaviour (Escape closes the dropdown, focus
jumps to `<main>` on skip-link, etc.). Scope: a separate test-tooling
increment.

### 3.5 End-to-end lifecycle test — DEFER

No single test drives the chain
`Registered → EmailVerified → first profile submit → PendingApproval →
admin Approve → Approved → App-audience sign-in`. Each hop is covered
in isolation; the cross-hop seam (e.g. an Approved visitor signs in on
the App audience) is not pinned. Recorded for a dedicated integration
test increment.

### 3.6 Release build verification — HARDENING

A Release-config build with the full warnings-as-errors switch (per
CLAUDE.md §9) should be the sign-off command. Sprint 1's running build
verification has been Debug-config; the suite has stayed green and
warning-free at Debug. Before deploy, run
`dotnet build -c Release SIMF.slnx` and capture the output.

### 3.7 No-IP rate-limit partition tightening — DEFER

H7 (D-062) left the `?? "unknown"` null-IP fallback unchanged because
tightening it to `Math.Min(5, PermitLimit)` broke ASP.NET TestServer
(which sets no `Connection.RemoteIpAddress`). The per-email partition
now bounds credential stuffing independent of IP key, so the value-add
of further tightening "unknown" is small. Revisit if a production
signal shows misrouted no-IP traffic abusing the fallback.

### 3.8 Notification dispatch outbox — DEFER

D-057 / D-065 leave a window where a state-flip commits but its
notification dispatch fails (the dispatch is intentionally outside the
DB transaction). The audit log surfaces the failure (H10 / D-065) but
the user message is missed until the operator re-triggers the flow.
An outbox-style guarantee for first dispatch is recorded for a later
operations sprint.

### 3.9 `myComment.txt` — DEFER

The owner's contemporaneous scratch list at the repo root contains
items that overlap Sprint 1 (e.g. "Test full cycle Login in cp,
create QR-google AUTH, check 2FA, Reset pwd, Email otp") and items
that go beyond it. The file remains uncommitted; it is the owner's
working note, not a controlling document. Drain or commit as the
owner sees fit.

---

## 4. Sign-off checklist

- [x] All commits on `feature/login-api` between Sprint 1 start and
      D-072 are local and authored as `SIMF Team` /
      `Co-Authored-By: Claude Opus 4.7`.
- [x] `dotnet build -c Debug SIMF.slnx` — 0 warnings, 0 errors.
- [x] `dotnet test SIMF.slnx` — 267 / 267 passing.
- [x] `docs/decisions/DECISIONS_LOG.md` runs D-001 → D-072 with each
      hardening item carrying a `Why:` reason and a `How to apply:` note.
- [x] CLAUDE.md project status line refreshed to reflect Sprint 1 closure.
- [ ] **Release-config build** (item 3.6).
- [ ] **Secret rotation decision** (item 3.1).
- [ ] **End-to-end lifecycle test** (item 3.5).
- [ ] **Push `feature/login-api` to origin** — waiting on the owner's call;
      do not push committed secrets without the rotation decision.

---

## 5. Next sprint

Per the programme plan, the next increment is the **User Management
module** — admin self-service for the `Other` / `Visitor` user types,
permission-driven navigation filtering (gate D1 / SIMF-CPD-001 OI-3),
and the User Management UI sat on the closed Login API foundation.
The six architecture SEV-1s in §3.2 should be addressed BEFORE the
User Management module's persistence layer grows on top of the
current `SimfIdentityDbContext` shape.
