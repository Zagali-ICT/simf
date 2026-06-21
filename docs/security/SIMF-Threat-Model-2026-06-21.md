# SIMF — Threat Model (STRIDE)

**Purpose:** satisfy NCA Secure Application-Development Standard **A4-13** ("use threat
modeling on the critical authentication, access-control, business-logic and key flows")
and provide a standing, auditable threat model for the NCA handover package.

**Method:** STRIDE (Spoofing, Tampering, Repudiation, Information disclosure, Denial of
service, Elevation of privilege) applied to SIMF's security-critical flows. Each row names
the threat, the existing control(s) in the codebase (with the responsible component), and
any residual risk / follow-up.

**Scope:** the .NET 10 API (FastEndpoints), Control Panel (Blazor Server), Website (Blazor
SSR) and Flutter app, over the two databases `SIMF_Identity` + `SIMF_App`.

**Date:** 2026-06-21 · **Companion docs:** `SIMF-Security-Assessment-2026-06-20.md`,
`SIMF-NCA-AppSec-Standard-GapAnalysis-2026-06-20.md`.

---

## 1. Trust boundaries & assets

**Trust boundaries**
1. Untrusted client (browser / Flutter app) → API (HTTPS, reverse proxy / IIS).
2. API → `SIMF_Identity` DB and `SIMF_App` DB (two physically separated databases; no
   cross-DB FK — D-157).
3. API → external services (SMTP, OpenAI/Anthropic AI provider).
4. CP / Website BFF (cookie session) → API (bearer token held server-side — BFF pattern).

**Key assets**
- Credentials & sessions: passwords (PBKDF2), OTP/account codes (keyed-HMAC at rest),
  JWT access/refresh tokens, security stamp, TOTP secrets.
- PII: national ID / Iqama / passport numbers + the ID-document image (image AES-256-GCM
  at rest; **the number columns are still plaintext — A2-10, open, Wave 6**), email, mobile.
- Authorization data: the permission catalogue, role grants, `perm`/`account_state` claims.
- Crypto keys: JWT signing key, ID-document AES key, SMTP credential (**exposed in git
  history — C1, owner ops**).
- Audit trail: `OperationLog` / `RowAudit` (append-only).

---

## 2. Flow A — Authentication & session (sign-in, 2FA, refresh, sign-out)

Components: `SignInService`, `SessionService`, `PasswordService`, `JwtTokenService`,
`JwtBearerSetup`, `AccountCodeHasher`, `SimfPasswordValidator`, `PasswordPolicy`.

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S** | Forged token (alg-confusion / `alg:none`) | HS256 pinned in `JwtBearerSetup` (`ValidAlgorithms`); issuer/audience validated | — |
| **S** | Credential stuffing / brute force | Account lockout 5 / 15 min; per-email 5/min + per-IP 600/min rate limits; generic 401 (no enumeration) | A7-8 60-min IP-lockout tier deferred (partial) |
| **S** | Weak/guessed password | `PasswordPolicy` (classes + repeat/sequence + leet common-list + identifier match), enforced both at request (FluentValidation) and centrally (`SimfPasswordValidator`) | A7-13 expiry / A7-20 history deferred (Wave 6) |
| **T** | Tamper with claims (role/state) | Claims are HS256-signed; role/identity change rolls the security stamp | — |
| **T** | OTP/reset-code theft from the DB | Codes stored as keyed-HMAC (`AccountCodeHasher`), emailed in plaintext only | — |
| **R** | Deny a sign-in / lockout happened | Every auth decision audited (`AuditEvents` SignIn*, RefreshToken*, AccessTokenRejected, **AuthorizationDenied** new in Wave 1) with IP + correlation id | TLS-handshake-failure event still missing (A9-15) |
| **I** | Token replay after sign-out / password change | Security-stamp revocation (constant-time compare); refresh-token rotation + reuse detection revokes the chain | — |
| **I** | Session id in URL / logs | Bearer in `Authorization` header / cookie; recording-stream token in query is scoped + `no-store` (L1) | — |
| **D** | Auth-endpoint flood; audit-write storm | Layered rate limits; per-IP throttle on bearer-rejection + 403 audit writes | — |
| **E** | Non-approved account reaching gated APIs | `account_state="Approved"` baked into every `perm:` policy (L2) | — |

## 3. Flow B — Authorization (per-page / per-action permissions)

Components: `PermissionCatalog`, `PermissionPolicyProvider` / `PermissionAuthorizationHandler`,
CP `RequirePermissionAttribute` + `AuthorizedAction`.

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S** | Act as another user (IDOR) | Actor resolved only from the signed `sub`; own-data ops are owner-scoped (e.g. `GetSavedCardAsync(ownerUserId,id)`) | — |
| **T** | Re-enable a UI-hidden action server-side | CP `AuthorizedAction` is UX-only; the API re-enforces every gate; build-time tests fail the build if a gate is missing | — |
| **R** | Deny a denied-access attempt | **Wave 1 (A1-12):** every 403 now writes an `Authorization.Denied` audit row (IP-throttled) | — |
| **I** | Probe scope via id enumeration | Cross-scope probes return the same 404; `Admin.ApprovalScopeMismatch` audit catches the pattern | — |
| **D** | Bulk data exfiltration by a perm-holder | Pagination + list caps | A1-14/A4-10 per-user/day export governor deferred |
| **E** | Privilege escalation | Default-deny handler; Administrator wildcard only on Admin-typed users; last-admin lockout guard; stamp rolls on role change | — |

## 4. Flow C — Badge / gate scan (physical access)

Components: `BadgeAuthService`, gate endpoints, `GateScan` audit, `QrIdMinter`.

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S** | Forged / replayed badge QR | High-entropy CSPRNG QR ids (`QrIdMinter`); badge-activation sets a password (keyed-HMAC code) | — |
| **T** | Tamper a scan result | Server-side decision; gate-scan idempotency store | — |
| **R** | Deny a scan / denial | `GateScanDenied` + scan audits with actor snapshot | — |
| **I** | Leak attendee PII via scan API | Per-action permission gating; minimal scan payload | — |
| **D** | Gate-endpoint abuse | Gate failure circuit-breaker (`Gate.FailureCircuitOpened/Closed`) + rate limits | — |
| **E** | Staff-only scan by a visitor | App role-gate (Guest/Visitor/Moderator/Staff) + API permission | — |

## 5. Flow D — Meeting requests (speaker & delegation G2G)

Components: speaker/delegation meeting endpoints, `SpeakerMeetingRequest*` /
`DelegationMeetingRequest*` services + audits.

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S** | Submit on behalf of another delegate | Requester taken from `sub`; eligibility (delegate-of-invited-country) checked server-side | — |
| **T** | Tamper target / slot | Server validates target = invited-non-self; slot owned by the speaker | — |
| **R** | Dispute a request/response | `SpeakerMeetingRequest*` / `DelegationMeetingRequest*` + admin list/view audits | — |
| **I** | Harvest requester emails | Per-action permission (`DelegationMeetings.View/Manage`); audited admin reads | — |
| **D** | Spam requests | Rate limits + workflow state machine | per-user/day cap (A4-10) deferred |
| **E** | Approve outside the review desk | Team accept/reject gated by `DelegationMeetings.Manage` | — |

## 6. Flow E — Crypto key & secret management

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S/I** | Stolen JWT/AES/SMTP key | Keys via Machine-scope env vars; boot guard rejects a too-short JWT key / committed default super-admin password | **C1/H1 — keys still in git history; rotate + purge + verify env (owner ops)** |
| **T** | Forged ID-document blob | AES-256-GCM (authenticated) — tag mismatch fails closed | crypto-failure not yet an audit event (A9-15) |
| **I** | PII at rest | ID-document image AES-256-GCM | **A2-10 — national-ID/Iqama/passport number columns still plaintext (Wave 6)** |
| **—** | Key lifecycle | (none) | **A2-7 — no issue/rotate/revoke/expire policy (owner)** |

## 7. Flow F — File upload (ID doc, avatar, presentations, media, Excel import)

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **T** | Malicious file disguised by extension | Magic-byte allow-lists (`ImageUploadValidation`, presentation PDF/OOXML/OLE2, XLSX ZIP) | — |
| **T** | Path traversal | Server-GUID filenames; `Path.GetFileName` + base-dir containment; **Wave 3** Unicode-normalises the name | L4 shared-guard lift latent |
| **I** | Webroot exposure / browser cache of PII | Uploads in non-web-served `App_Data`; presentations served `attachment`; **Wave 1** `no-store` on ID-doc responses | — |
| **D** | Decompression bomb / oversize | Header-only ≤60 MP pre-check + decode semaphore (H3); per-type size caps | — |
| **—** | Known malware in an allowed type | (none) | **A6-18 — AV scanning deferred (Wave 7)** |

## 8. Flow G — Mobile app (Flutter)

| STRIDE | Threat | Control(s) | Residual / follow-up |
|---|---|---|---|
| **S/I** | MITM of app traffic | **Held open by owner: app-wide TLS trust-all (C2) + self-signed cert (H2)** — fix = CA cert then remove the override + add pinning (A11-15/22) | **OPEN — owner-held** |
| **I** | Screenshot / app-switcher leak | **Wave 5** FLAG_SECURE (native layer — git-ignored `android/`, owner to persist) | iOS snapshot pending (no `ios/` in repo) |
| **I** | Shared-PII temp file lingers | **Wave 5 (A11-3)** purge of share temp files | — |
| **I** | Secrets at rest on device | Keystore/Keychain via `flutter_secure_storage`; no SQLite/SharedPreferences PII | — |
| **T/E** | Tampered / rooted device | (none) | A11-7 root/JB detection + A11-26 anti-debug deferred |

---

## 9. Top residual risks (carried to the gap report / owner actions)

1. **C2 / H2** — mobile TLS trust-all + self-signed cert (owner-held; CA cert then remove override + pin).
2. **C1 / H1** — JWT/AES/SMTP keys + super-admin default in git history; rotate + purge + verify env.
3. **A2-10** — national-ID/Iqama/passport numbers plaintext at rest (Wave 6, after the worker's App-schema refactor lands).
4. **A6-18** — no AV scan of uploaded files (Wave 7).
5. **A2-7** — no key-management lifecycle policy (owner).
6. Deferred hardening: A7-8 IP-lockout, A7-13/20/31 password lifecycle, A1-14/A4-10 export governor, A11-7/26 mobile anti-tamper, A9-15 crypto/TLS-failure audit events.

This document should be reviewed whenever a new security-critical flow is added (per A4-13)
and re-attested before the production publish / NCA handover.
