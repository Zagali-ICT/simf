# SIMF Platform — Security Assessment & Vulnerability Report

| | |
|---|---|
| **Document** | SIMF Security Assessment (Pentest + Vulnerability Assessment) |
| **Date** | 2026-06-20 |
| **Assessor** | Internal automated white-box + non-destructive recon (Claude Code) |
| **Authorisation** | Owner-authorised assessment of the owner's own systems |
| **Targets (live)** | `https://simf.zagali-ict.com` (Website) · `https://simf_api.zagali-ict.com` (API) · `https://simf_app.zagali-ict.com` (App/static) — all → `173.201.37.122` |
| **Codebase** | `D:\SIMF\System\V1.0.0` (.NET 10 API + Blazor CP/Website + Flutter app) |
| **Posture** | **Non-destructive.** White-box source review + read-only live recon only. No active exploitation, no payloads that mutate data, no brute-force, no DoS against production. |
| **Standards** | OWASP Top 10 2021 · OWASP API Security Top 10 2023 · OWASP Mobile Top 10 2024 · NCA Essential Cybersecurity Controls (ECC-1:2018) |

> **Note on the third URL.** `simf_web.zagali-ict.com` (as written in the request) **does not resolve** in DNS. The three hosts that resolve are `simf`, `simf_api`, and `simf_app` — all to the same IP behind a Host-header reverse proxy (IIS 10). This report covers those three.

> **🔒 Redaction note (2026-07-27, owner decision).** This report originally quoted three credential values verbatim in findings **C1** and **H1**: the SMTP relay host, the SMTP user / sending address, and the super-admin bootstrap password (twice). Those values were **redacted on 2026-07-27** and replaced in place with the configuration **key path** each one belongs to (`Email:Host`, `Email:User` / `Email:FromAddress`, `SuperAdmin:TempPassword`) plus the `SIMF_`-prefixed environment variable that now supplies it. Every finding keeps its evidence — the file, the line, the key and the impact are unchanged; only the recoverable value is gone.
>
> **Redaction is not rotation.** Removing a value from this document revokes nothing. All three credentials remain in git history and in any existing clone or build artifact, so **rotation at the provider is a separate action and is still outstanding** — see the operator items in §7 and §9.

---

## 1. Executive summary

The SIMF backend is, on the whole, a **well-engineered and security-conscious codebase** — layered rate-limiting, algorithm-pinned JWTs with security-stamp revocation, CSPRNG everywhere, AES-256-GCM encryption of national-ID images, an explicit (never-wildcard) CORS allow-list, fail-fast boot guards, a roles-only per-page/per-action permission system, and disciplined logging. The data layer has **no SQL injection, no XXE, no unsafe deserialisation, and no user-driven SSRF**.

The risk is concentrated in a small number of **high-impact secret-management and transport issues**, most of them artefacts of the PoC/event push and explicitly flagged in-code as "revert before NCA handover":

- **2 Critical** — live secrets committed to git (SMTP password, JWT key, national-ID AES key); and an app-wide TLS-validation bypass shipping in the Flutter release build.
- **3 High** — committed super-admin bootstrap password + working TOTP seed; a self-signed production TLS certificate (machine-name CN, no CA chain); and an unbounded image-decode denial-of-service.
- **7 Medium**, **~11 Low**, plus informational items.

**None of these require a code rewrite.** The Critical/High items are fixed by rotating secrets, removing them from source, issuing a real CA certificate, removing the TLS bypass, and adding a few input/resource guards. **Priority #1 (do today): rotate the four committed secrets and purge them from history** — they are exposed to anyone with repository or build-artifact access.

### Severity counts

| Severity | Count |
|---|---|
| 🔴 Critical | 2 |
| 🟠 High | 3 |
| 🟡 Medium | 7 |
| 🔵 Low | 11 |
| ⚪ Informational | 5 |

---

## 2. Live reconnaissance (non-destructive)

| Check | Result | Assessment |
|---|---|---|
| DNS | `simf`, `simf_api`, `simf_app` → `173.201.37.122`; `simf_web` → NXDOMAIN | All hosts share one box (GoDaddy range) |
| TLS protocols (API) | TLS 1.0/1.1 **disabled**, TLS 1.2 + 1.3 enabled | ✅ Good |
| TLS certificate (API) | `subject=CN=WIN-MAP9VAMAU4Q`, `issuer=CN=WIN-MAP9VAMAU4Q` (self-signed), expires 2026-08-07 | 🟠 Self-signed, hostname mismatch (H2) |
| API error verbosity | `/nonexistent` → empty body, no stack trace | ✅ Good (ErrorHandlingMiddleware) |
| Swagger in prod | `/swagger` → **401** (Basic-auth gated) | ✅ Good (as designed, D-355) |
| `/health` | 200, public | ⚪ Minor info exposure |
| Method handling | `GET` on POST route → 405 `Allow: POST` | ✅ Correct |
| Security headers — Website (`simf`) | HSTS (30d), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy` | 🟡 Good set, but short HSTS + no CSP (M7) |
| Security headers — API & App hosts | **none** | 🟡 Missing (M7) |
| Banner disclosure (all hosts) | `Server: Microsoft-IIS/10.0`, `X-Powered-By: ASP.NET` | ⚪ Tech/version disclosure (INFO-1) |

---

## 3. Findings register

| ID | Finding | Severity | OWASP | NCA ECC |
|---|---|---|---|---|
| C1 | Live secrets committed in `appsettings.Development.json` (SMTP pw, JWT key, ID-doc AES key) | 🔴 Critical | A02 / A05 / A07 | 2-8, 2-7, 2-4 |
| C2 | Flutter ships an app-wide trust-all TLS bypass (MITM) in release | 🔴 Critical | M5 / A02 | 2-6, 2-8 |
| H1 | Super-admin temp password + working TOTP seed committed in `appsettings.json` | 🟠 High* | A07 / A05 | 2-2, 2-8 |
| H2 | Self-signed production TLS certificate (machine-name CN, no CA chain) | 🟠 High | A02 | 2-8, 2-5 |
| H3 | Unbounded image decode → decompression-bomb DoS | 🟠 High | A04 / API4 | 2-15 |
| M1 | Asset image upload: declared-type-only (no magic-byte) + served anon without `nosniff` → stored-XSS path | 🟡 Medium | A03 / A05 | 2-15 |
| M2 | Speaker-presentation upload: no content-type/extension allow-list | 🟡 Medium | A04 (CWE-434) | 2-15 |
| M3 | Email/reset OTP codes stored in plaintext at rest | 🟡 Medium | A02 | 2-8 |
| M4 | AI prompt-hash HMAC dev-fallback boot guard never wired; dead/misleading `AiAssistant` config | 🟡 Medium | A02 / A05 | 2-8, 2-15 |
| M5 | `System.IdentityModel.Tokens.Jwt` version drift (Web 8.2.1 vs 8.18.0) | 🟡 Medium | A06 | 2-10 |
| M6 | CP `SessionModerationDesk` gated only `[Authorize]`, not `[RequirePermission]`; action buttons unwrapped | 🟡 Medium | A01 | 2-2 |
| M7 | Missing/inconsistent HTTP security headers; no CSP anywhere; short HSTS | 🟡 Medium | A05 | 2-15 |
| L1 | Recording-stream token in query string, no user binding, 180-min lifetime | 🔵 Low | A01 / API2 | 2-2 |
| L2 | `RequireApprovedAccount` gate is opt-in, not central (notification endpoints ungated) | 🔵 Low | A01 / API5 | 2-2 |
| L3 | Open redirect via admin-set asset external link (302 to arbitrary host), anonymous | 🔵 Low | A01 (CWE-601) | 2-15 |
| L4 | Path-traversal containment guard inconsistent across filesystem storages (latent) | 🔵 Low | A01 (CWE-22) | 2-15 |
| L5 | OpenAI provider echoes full error body to logs | 🔵 Low | A09 | 2-12 |
| L6 | `/admin/logs` full download, gated but unredacted | 🔵 Low | A09 / A01 | 2-12, 2-2 |
| L7 | `AllowedHosts: "*"` (Host-header injection latent) | 🔵 Low | A05 | 2-15, 2-5 |
| L8 | Default app key `simf-dev-app-key` baked in when build omits `--dart-define` | 🔵 Low | M1 / M8 | 2-6 |
| L9 | `android:allowBackup` not disabled | 🔵 Low | M9 / M8 | 2-6 |
| L10 | Latent DOM-XSS: image URLs interpolated into CSS `background-image` with weak escaping | 🔵 Low | A03 | 2-15 |
| L11 | Byte-serving endpoints (avatar/media/ID/asset) omit `X-Content-Type-Options: nosniff` | 🔵 Low | A05 | 2-15 |
| INFO-1 | Server/tech version disclosure (`Server`, `X-Powered-By`) | ⚪ Info | A05 | 2-15 |
| INFO-2 | Public `/health` endpoint | ⚪ Info | A05 | 2-15 |
| INFO-3 | Per-session moderator authz enforced imperatively, not declaratively | ⚪ Info | A01 | 2-2 |
| INFO-4 | No SBOM / no `dotnet list package --vulnerable` CI gate; pre-1.0 `FaceAiSharp` supply-chain watch | ⚪ Info | A06 | 2-10 |

\* H1 becomes **Critical** if production never set the `SIMF_SuperAdmin__*` env-var overrides (see finding).

---

## 4. Detailed findings

### 🔴 C1 — Live secrets committed in `appsettings.Development.json`

**Evidence (verified):** `src/Backend/SIMF.Api/appsettings.Development.json`, git-tracked since the first scaffold commit (`9252a57`), **not** covered by `.gitignore` (which only excludes `appsettings.Production.json` and `*.Local.json`):
- L18 — `Email:Password` (SMTP password, `uYVD…`) for the sending mailbox **[value redacted 2026-07-27 — `Email:User` / `Email:FromAddress`]** on a third-party SMTP relay **[host redacted 2026-07-27 — `Email:Host`, port 587]**. All three are now supplied out of the repo via `SIMF_Email__Password` / `SIMF_Email__User` / `SIMF_Email__Host`.
- L23 — `Jwt:SigningKey` = `vRCV…89Id` (HMAC-SHA256 token-signing key)
- L29 — `Storage:UserIdDocumentEncryptionKey` = `0YOk…/LQ=` (the AES-256-GCM key protecting **every uploaded national-ID/Iqama image**)

**Impact.** Anyone with repository read access or a build artifact holds three real secrets:
- The **third-party SMTP credential** (`Email:Host` / `Email:User` / `Email:Password`; relay provider **[redacted 2026-07-27]**) is environment-independent — it authenticates to that mail server regardless of which SIMF environment loads the file. If still valid, an attacker can send mail as the SIMF sending address **[redacted 2026-07-27 — `Email:FromAddress`]** (phishing SIMF/RSNF attendees) and read the mailbox if IMAP is on.
- The **JWT signing key** forges valid tokens (including admin) **if** production ever reused it or was run in Development mode.
- The **national-ID AES key** decrypts the most sensitive PII in the system **if** production reused it.

Production does **not** load `appsettings.Development.json` (ASPNETCORE_ENVIRONMENT=Production), and prod secrets arrive via `SIMF_`-prefixed env vars — so this is primarily a *source-control exposure*, not proof of prod compromise. But the SMTP credential is live independent of that, and key-reuse cannot be ruled out from outside the server.

**OWASP** A02 Cryptographic Failures / A05 Security Misconfiguration / A07 Auth Failures · **NCA ECC** 2-8 Cryptography (key management), 2-7 Data Protection, 2-4 Email Protection.

**Remediation (today):**
1. Treat all three as compromised and **rotate now**: the SMTP relay password at the provider (`SIMF_Email__Password`); `SIMF_Jwt__SigningKey` (invalidates current sessions — acceptable); the ID-document AES key (requires re-encrypting existing files).
2. `git rm --cached src/Backend/SIMF.Api/appsettings.Development.json`, add it to `.gitignore`, and commit a `appsettings.Development.json.template` with empty values.
3. **Purge from git history** (BFG / `git filter-repo`) — the values are exposed in every clone.
4. Confirm on the server that prod env vars do **not** equal these committed values.

---

### 🔴 C2 — Flutter app ships an app-wide trust-all TLS bypass

**Evidence (verified):** `src/Mobile/simf_app/lib/core/net/self_signed_api_tls_io.dart:22-28` overrides `badCertificateCallback` to return `true` for **any** certificate, host, and port; installed via `HttpOverrides.global` at `main.dart:28` (`installSelfSignedApiTlsBypass()`) with **no `kReleaseMode` guard** → present in release APKs. Because it is global, it disables TLS validation for *every* host the app talks to (API, YouTube, video, images), not just the API. The file header itself states: *"removes MITM protection app-wide … MUST be reverted … before the production publish / NCA handover."* The `main.dart:27` comment ("configured API host ONLY") is stale and incorrect.

**Impact.** Any on-path attacker (rogue Wi-Fi, ARP/DNS spoof, malicious proxy) can present a self-signed cert, terminate TLS, and **read/modify all app traffic** — bearer access/refresh tokens, `X-App-Key`, national-ID/Iqama/passport/mobile PII, and gate-scan/attendance data → full session hijack and silent tampering.

**OWASP** Mobile M5 Insecure Communication (+ M3) / A02 · **NCA ECC** 2-6 Mobile Devices Security, 2-8 Cryptography. A trust-all client is a direct ECC TLS-validation violation.

**Remediation.** This is the *dependent* fix of H2 — issue a real CA certificate for a proper (no-underscore) API hostname, then **delete the global override**. If a self-signed cert is unavoidable during the PoC, scope trust to that one leaf cert via `SecurityContext.setTrustedCertificatesBytes` **and** gate it behind `if (!kReleaseMode)` so it can never ship; add SPKI pinning for production. Make "no trust-all in release" a build/release gate, not a code comment.

---

### 🟠 H1 — Super-admin temp password + working TOTP seed committed

**Evidence:** `src/Backend/SIMF.Api/appsettings.json:34-38` — `SuperAdmin.Email = superadmin@zagali-ict.com`, `TempPassword = "[REDACTED - supply via SIMF_SuperAdmin__TempPassword]"`, `TotpSecret = "dbji csx7 …"` (a valid base32 authenticator seed). Seeded by `IdentitySeeder.cs` (`CreateSuperAdminAsync` sets `PasswordChangeRequired=true` at first creation; the TOTP secret is re-asserted on deploy while 2FA is enabled). Override path exists: `Program.cs:36` `AddEnvironmentVariables("SIMF_")` lets `SIMF_SuperAdmin__TempPassword`/`__TotpSecret` win, and `deploy/set-env-api.ps1` lists them as required-but-skips-empty.

**Impact — conditional:**
- If prod **did** set the `SIMF_SuperAdmin__*` env vars before first boot → the committed values are inert defaults (residual: git exposure of an example). 
- If prod **did not** (the template skips empty values, so appsettings wins) → the **live super-admin is `superadmin@zagali-ict.com` / `[REDACTED - supply via SIMF_SuperAdmin__TempPassword]` with a known TOTP seed**. The committed seed yields valid 6-digit codes, so MFA gives zero protection against a repo-access adversary → full `Administrator` (`perm:*`) compromise. `PasswordChangeRequired=true` only forces a reset *after* a successful first login; it doesn't stop the attacker logging in first.
**Evidence:** `src/Backend/SIMF.Api/appsettings.json:34-38` — `SuperAdmin.Email = superadmin@zagali-ict.com`, `TempPassword = ` **[value redacted 2026-07-27 — `SuperAdmin:TempPassword`, now supplied via `SIMF_SuperAdmin__TempPassword`]** (a short, guessable-class password), `TotpSecret = "dbji csx7 …"` (a valid base32 authenticator seed). Seeded by `IdentitySeeder.cs` (`CreateSuperAdminAsync` sets `PasswordChangeRequired=true` at first creation; the TOTP secret is re-asserted on deploy while 2FA is enabled). Override path exists: `Program.cs:36` `AddEnvironmentVariables("SIMF_")` lets `SIMF_SuperAdmin__TempPassword`/`__TotpSecret` win, and `deploy/set-env-api.ps1` lists them as required-but-skips-empty.

**Impact — conditional:**
- If prod **did** set the `SIMF_SuperAdmin__*` env vars before first boot → the committed values are inert defaults (residual: git exposure of an example). 
- If prod **did not** (the template skips empty values, so appsettings wins) → the **live super-admin is `superadmin@zagali-ict.com` with the committed `SuperAdmin:TempPassword` [value redacted 2026-07-27] and a known TOTP seed**. The committed seed yields valid 6-digit codes, so MFA gives zero protection against a repo-access adversary → full `Administrator` (`perm:*`) compromise. `PasswordChangeRequired=true` only forces a reset *after* a successful first login; it doesn't stop the attacker logging in first.

**This is the single most important thing to verify on the server.** It is rated High but is **Critical** in the un-overridden case.

**OWASP** A07 / A05 · **NCA ECC** 2-2 IAM (default/initial credentials), 2-8 Cryptography.

**Remediation.** Verify the prod env overrides are set to non-default values and the live super-admin password was rotated post-first-login. Blank the literals in `appsettings.json` (match the empty `Jwt:SigningKey` pattern — the seeder no-ops on blank). Stop the seeder re-asserting a TOTP secret on an existing account; require the super-admin to re-enrol its own authenticator. Add a Production boot guard that refuses to start if `SuperAdmin:TempPassword` equals the known committed default. Purge from history.

---

### 🟠 H2 — Self-signed production TLS certificate

**Evidence (live):** API host presents `CN=WIN-MAP9VAMAU4Q` (the server's machine name), issued by itself, no chain of trust; underscore hostnames (`simf_api`, `simf_app`) are not valid public-CA subjects. This is the **root cause** of C2 (the app had to disable validation to talk to it) and produces browser/desktop trust warnings.

**Impact.** No authenticated server identity → MITM is not detectable by clients; conditions users/operators to "click through" cert warnings; blocks HSTS preload and modern client hardening.

**OWASP** A02 · **NCA ECC** 2-8 Cryptography, 2-5 Network Security. NCA requires valid, trusted TLS for internet-facing services.

**Remediation.** Obtain a publicly-trusted CA certificate (e.g. Let's Encrypt/commercial) for **DNS-valid hostnames without underscores** (e.g. `api.zagali-ict.com`, `app.zagali-ict.com`). Migrate the hosts/proxy bindings, update the app/CP/Web base URLs, then remove the C2 bypass. Set HSTS to ≥1 year with `includeSubDomains` once all subdomains are HTTPS.

---

### 🟠 H3 — Unbounded image decode (decompression bomb → DoS)

**Evidence:** `src/Backend/SIMF.Infrastructure/Identity/FaceAiSharpFaceDetectionService.cs:62` calls `Image.Load<Rgb24>(imageBytes)` (SixLabors.ImageSharp, default `Configuration` — no `MaxAllocationInBytes`, no pixel ceiling). Reached from avatar upload and the walk-in/admin ID face gate. Upload caps are 2 MB (avatar) / 5 MB (ID), and validation checks only header magic bytes, not dimensions. ONNX inference then runs under a process-wide `lock`.

**Impact.** A ~2–5 MB highly-compressed image (e.g. 30000×30000) decodes to multiple GB of `Rgb24`; a few concurrent uploads exhaust memory (OOM) and serialise behind the inference lock — a cheap availability attack by any approved visitor.

**OWASP** A04 Insecure Design / API4 Unrestricted Resource Consumption · **NCA ECC** 2-15 Web App Security (resource limits).

**Remediation.** Clone `Configuration.Default`, set `MaxAllocationInBytes` (e.g. 256 MB) and a width×height guard (reject > ~8000×8000) via `DecoderOptions`; `MaxFrames = 1`; bound concurrency with a `SemaphoreSlim` instead of an unbounded queue behind the lock.

---

### 🟡 Medium findings

- **M1 — Asset image upload trusts the client content-type, served anonymously without `nosniff`.** `AssetService.cs:360` (`ValidateUpload`) checks only the declared `contentType` + size (no magic-byte check, unlike avatar/ID uploads), persists it (`:69`), and echoes it from the **anonymous** `PublicFetchAssetEndpoint` (`AssetEndpoints.cs:53,199-217`) with no `X-Content-Type-Options: nosniff`. A polyglot (HTML/SVG-with-script declared `image/png`) can be MIME-confused into **stored XSS** on the public origin. *Fix:* run `ImageUploadValidation.IsAllowedImage` on asset uploads; add a global `nosniff` + CSP; force `attachment` for PDFs.
- **M2 — Speaker-presentation upload has no type allow-list.** `AdminSpeakerPresentationService.cs:43-101` enforces only a 50 MB cap + filename sanitise; stores the original extension and client content-type. *Mitigated today* (server-GUID names, `App_Data/presentations` not web-served, download forced as `attachment`), so execution/XSS is largely blocked — but add a magic-byte allow-list (PDF/PPTX/DOCX) as defense-in-depth and regression insurance. (CWE-434.)
- **M3 — OTP/reset codes stored in plaintext.** `AccountCode.Code` is a bare string (`AccountCode.cs:22`), unlike refresh tokens/recovery codes which store SHA-256 hashes. DB read access (injection elsewhere, leaked backup, over-privileged account, insider) yields live reset codes → account takeover without mailbox access. Brute force is bounded (5 attempts, 10-min expiry, single-use, constant-time, capped reissue), so it's Medium. *Fix:* hash with the existing `OpaqueToken.Hash` pattern — value-only change, no schema migration.
- **M4 — AI prompt-hash HMAC dev-fallback guard never wired; dead `AiAssistant` config.** The intended guard (`AiAuditDetail.IsHmacKeyDevFallback`, documented at `DependencyInjection.cs:400-403`) is **never read** in `Program.cs` → in prod without `Ai:PromptHash:Secret`, the module uses a publicly-derivable HMAC key (`AiAuditDetail.cs:53-54`), defeating preimage resistance for the audit fingerprints. Separately, the `AiAssistant` block (`appsettings.json:77-84`) is bound by **no code** (the code binds `Ai`) — an operator could leak a key into the wrong, ignored section. *Fix:* wire the boot guard (fail-fast in prod, like the JWT/proxy guards); delete the dead block.
- **M5 — JWT library version drift.** `SIMF.Web.csproj:22` pins `System.IdentityModel.Tokens.Jwt 8.2.1` vs `8.18.0` elsewhere. The Web project only decodes (doesn't validate) the API JWT, so it's not in a forgery path — but running an auth library two-dozen patches behind on one deployable is a patch/CVE liability. *Fix:* bump to 8.18.0; introduce `Directory.Packages.props` for central versioning (csproj change → owner approval per project rule).
- **M6 — CP `SessionModerationDesk` authz gap.** `SessionModerationDesk.razor:4-6` is gated only `[Authorize]` (not `[RequirePermission]`), and its Hide/Unhide/Push buttons aren't wrapped in `<AuthorizedAction>` — violating the project's own hard rule. The API still enforces per-session moderator/Administrator (so a non-permitted admin's clicks 403), keeping it Medium, but the CP convention is bypassed and `CpNavigationPermissionTests` doesn't cover directly-routed pages. *Fix:* add/seed `Questions.Moderate`, switch the attribute, wrap the buttons, add a permission-enforcement test.
- **M7 — Missing/inconsistent HTTP security headers.** The API and App hosts send **no** security headers; the Website has a good set but **HSTS only 30 days, no `includeSubDomains`/`preload`, and no Content-Security-Policy anywhere**. *Fix:* one global headers middleware (or proxy config) emitting `nosniff`, `X-Frame-Options`/frame-ancestors, a tuned CSP, `Referrer-Policy`, `Permissions-Policy`, and long HSTS across all hosts.

### 🔵 Low findings (summary)

| ID | Summary | Fix |
|---|---|---|
| L1 | Recording-stream JWT in `?access_token=`, no `sub`, 180-min life — leaks via logs/Referer; lets anyone stream that one published recording | Shorten to 5–10 min, add `sub`, `Cache-Control: no-store`; move to per-segment signed URLs |
| L2 | `RequireApprovedAccount` is opt-in per endpoint; notification endpoints declare no policy (own-scoped, low impact) | Bake the approval claim into the `perm:` policy + a default app-endpoint configurator |
| L3 | Anonymous asset endpoint 302-redirects to any admin-set external URL (open redirect / phishing) | Return URL in JSON for client render, or allow-list media hosts |
| L4 | `Path.Combine(_root, name)` without containment in 4 filesystem stores (callers pass server-GUID names today — latent) | Lift `EncryptedUserIdDocumentStorage.ResolveSafe` guard into a shared base |
| L5 | `OpenAiProvider.cs:63-65` logs full provider error body | Log status + length-capped, redacted body |
| L6 | `/admin/logs` download is gated + audited but unredacted (emails/PII) | Keep `Logs.View` narrow; redact on download |
| L7 | `AllowedHosts:"*"` (CORS unaffected — that's a strict allow-list) | Set real hostnames per env |
| L8 | App default key `simf-dev-app-key` if build omits `--dart-define` | Fail/warn the prod build if unset; don't treat `X-App-Key` as auth |
| L9 | `android:allowBackup` defaults to true (tokens are in Keystore; config data exposed) | Set `allowBackup="false"` + backup rules |
| L10 | Website `index.html` interpolates image URLs into CSS `url(...)` with single-quote-only/no escaping (fields are server-controlled today — latent stored DOM-XSS if ever admin free-text) | `esc()` + scheme allow-list before interpolation; add CSP |
| L11 | Avatar/media/ID/asset byte endpoints omit `nosniff` (types are validated, so low) | Global `nosniff` middleware |

### ⚪ Informational

- **INFO-1** `Server: Microsoft-IIS/10.0` + `X-Powered-By: ASP.NET` on all hosts — remove via IIS `<httpProtocol>` / `<security/requestFiltering>`.
- **INFO-2** Public `/health` — acceptable; ensure it returns no dependency detail.
- **INFO-3** Per-session moderator gate is imperative (`SessionQuestionEndpoints.cs:61-215`), not declarative — correct today; promote to a first-class policy so it's uniformly testable.
- **INFO-4** No SBOM / vulnerable-package CI gate; `FaceAiSharp.Bundle 0.6.35` (pre-1.0, bundles ONNX weights) + native `onnxruntime` are the top supply-chain watch items. Add `dotnet list package --vulnerable --include-transitive` + a CycloneDX SBOM to CI; pin/verify model hashes.

---

## 5. Strong controls observed (what's already done right)

- **JWT:** issuer/audience/lifetime validated, **algorithm pinned to HS256** (blocks `alg:none`/confusion), signing-key length gated at boot, constant-time security-stamp revocation, password-change/sign-out roll the stamp.
- **Permissions:** roles-only, JWT-baked `perm` claims, `Administrator = "*"`, dynamic `perm:` policy provider; ~90 CP pages carry `[RequirePermission]` (M6 is the lone exception); build-time tests guard nav coverage.
- **IDOR/BOLA:** user-owned reads/writes resolve the actor from the token `sub` and use `…Mine`/owner-scoped methods — no own-by-route-id access to others' data. Contact-share requires a high-entropy rotatable token; vCard correctly omits the badge QrId.
- **Crypto:** AES-256-GCM (authenticated) for national-ID images with per-write CSPRNG nonce + key gate; CSPRNG for **all** OTPs/tokens/recovery codes/QR ids/device challenges (no `new Random()`, no Guid-as-secret); refresh/recovery codes hashed at rest; ASP.NET Identity PBKDF2 + lockout.
- **Injection-class:** EF LINQ throughout (no raw/dynamic SQL; the one raw statement is a constant), whitelist `switch` sorting (no column injection), no XXE/`BinaryFormatter`/polymorphic JSON, anchored/linear validation regexes (no ReDoS), strict YouTube/stream-URL allow-list, no user-driven SSRF.
- **Uploads:** server-GUID filenames, `App_Data/*` not web-served, magic-byte + size validation on avatar/ID, `attachment` download disposition.
- **Web/CP token handling (BFF):** API tokens kept server-side in the encrypted auth cookie — never exposed to JS; cookies `HttpOnly` + `SameSite=Lax` + `Secure` in prod; no reflected-`returnUrl` open redirect; output-encoding helper applied to text/`<img src>`.
- **Flutter (aside from C2):** tokens in Keystore-backed `flutter_secure_storage`; biometric/Face-ID is a server-validated ES256 device-key flow (not a local bypass); scanned QR treated as opaque; no hardcoded secrets (all `--dart-define`); cleartext HTTP scoped to the debug manifest only.
- **Platform:** layered rate-limiting (global per-IP + per-email + per-admin) with audited rejections, fail-fast boot guards, forwarded-headers restricted to known proxies, CORS never wildcard, Swagger off/Basic-auth-gated in prod, TLS 1.2/1.3 only, disciplined logging (no secrets), AI input caps + PII/secret redaction.

---

## 6. NCA ECC posture snapshot

| ECC domain | Status | Driven by |
|---|---|---|
| 2-2 Identity & Access Management | ⚠️ Mostly strong; fix H1, M6, L1/L2 | committed admin creds, CP gap |
| 2-7 Data & Information Protection | ⚠️ Strong at rest; fix C1 (key in git) | ID-doc AES key exposure |
| 2-8 Cryptography | ⚠️ Strong primitives; fix C1/H1/H2/M3/M4 | committed keys, self-signed TLS, plaintext OTP |
| 2-6 Mobile Devices Security | ❌ Fix C2, L8, L9 | TLS bypass in release |
| 2-10 Vulnerability Management | ⚠️ Add SBOM/CI gate; fix M5 | dependency drift, no SBOM |
| 2-12 Event Logs & Monitoring | ✅ Strong; minor L5/L6 | unredacted edges |
| 2-15 Web Application Security | ⚠️ Fix H3, M1/M2/M7, L3/L10/L11 | DoS, headers, upload hardening |
| 2-5 Network Security | ⚠️ Fix H2; INFO-1 | self-signed cert, banner disclosure |

---

## 7. Remediation roadmap (prioritised)

**Now (pre-handover blockers):**
1. **C1** — Rotate the 4 committed secrets (SMTP pw, JWT key, ID-doc AES key, + H1 super-admin), untrack `appsettings.Development.json`, purge history.
2. **H1** — Verify prod `SIMF_SuperAdmin__*` overrides + confirm the live admin password was rotated; blank the literals; add the default-password boot guard.
3. **H2 → C2** — Issue a real CA cert for no-underscore hostnames; then delete the Flutter trust-all override (gate any residual self-signed trust behind `!kReleaseMode`).

**This sprint:**
4. **H3** — ImageSharp allocation/dimension cap + bounded concurrency.
5. **M1/M2/M7/L11** — magic-byte parity on asset/presentation uploads + one global security-headers middleware (`nosniff`, CSP, long HSTS, frame-ancestors, Referrer/Permissions-Policy).
6. **M3** — hash `AccountCode.Code` at rest. **M4** — wire the AI-HMAC boot guard + delete dead config. **M6** — close the CP moderation authz gap.

**Backlog / hardening:**
7. M5 (JWT version + central package versioning), L1–L10, INFO-1..4 (banner suppression, SBOM + vulnerable-package CI gate, declarative moderator policy, central approval gate).

---

## 8. Scope, method & limitations

- **White-box source audit** of the full `src/` tree (API, Infrastructure, Application, Domain, ControlPanel, Website, shared libs, Flutter app) via four parallel security-engineer passes (auth/authz/IDOR; injection/files/SSRF; secrets/crypto/deps/logging; client Blazor + Flutter). `.claude/worktrees/`, `bin/`, `obj/` excluded.
- **Non-destructive live recon** against the three resolving hosts: DNS, TLS protocol/cert, HTTP headers, method handling, error verbosity, Swagger/health exposure. No payloads, no auth attempts, no fuzzing, no brute-force, no data mutation, no load.
- **Not performed (out of agreed posture):** active exploitation, injection payloads against live data, credential brute-force, DoS validation, authenticated API fuzzing. The C2/H1/H3 exploitabilities are demonstrated **from code**, not by attacking production.
- **Server-side unknown:** whether prod env vars override the committed defaults (H1) can only be confirmed on the host — recommended as the first operator action.
- All findings cite real `file:line` evidence read this session; the two Criticals were independently re-verified against the files and git before rating.

---

## 9. Post-assessment update (2026-06-20)

### Remediation applied in code (owner-approved subset: C1, H1, H3)
- **C1** — the three live secrets in `appsettings.Development.json` (SMTP password, JWT key, ID-doc AES key) blanked to `""`. *Source exposure closed for future commits; the values remain in git history — **rotation is still required** (operator action).*
- **H1** — `SuperAdmin.TempPassword`/`TotpSecret` in `appsettings.json` blanked; `Program.cs` now refuses to start in Production if `SuperAdmin:TempPassword` equals the committed default. *Server env-var verification + live-admin rotation still required (operator action).*
- **H3** — `FaceAiSharpFaceDetectionService` now rejects over-`MaxDecodePixels` (40 MP) images via a header-only `Image.Identify` pre-check and bounds concurrent decodes with a `SemaphoreSlim(2)`.
- **Verification:** Release build 0 warnings / 0 errors; full suite **1408 tests passed, 0 failed**.
- **Held by owner decision:** C2 (Flutter TLS bypass) and H2 (self-signed certificate) — unchanged pending the CA-certificate rollout.

### New finding surfaced during the verification build

#### 🟡 M8 — Vulnerable transitive dependency `SQLitePCLRaw.lib.e_sqlite3 2.1.11` (high-severity advisory) breaks the Release build
- **Evidence:** `dotnet build -c Release` fails restore with `error NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability` (GHSA-2m69-gcr7-jv3q), pulled transitively into `tests/SIMF.Api.Tests/SIMF.Api.Tests.csproj` (EF Core SQLite test provider). `NuGetAudit` + `TreatWarningsAsErrors` correctly promote it to a build error.
- **Impact:** Test-scoped (the native SQLite lib is not shipped to production), so runtime exposure is low — **but the build is currently red for every developer/CI run** until the package is bumped. Demonstrates the value of the audit gate (INFO-4) — it caught a real advisory.
- **OWASP** A06 Vulnerable & Outdated Components · **NCA ECC** 2-10 Vulnerability Management.
- **Remediation:** Add an explicit `SQLitePCLRaw.bundle_e_sqlite3` (or `…lib.e_sqlite3`) PackageReference pinned to a patched version (≥ the advisory's fixed release), or bump the EF Core SQLite test package that pulls it. Requires a `.csproj`/package edit (owner-gated per project rule §1.7).
- **Confidence:** High (reproduced in the build).
- **Status (2026-06-20):** **Fixed** in `ed1c05c` — pinned `SQLitePCLRaw.bundle_e_sqlite3 3.0.3` (native `e_sqlite3 3.50.3`, SQLite ≥ 3.50.2) in `SIMF.Api.Tests`. Release build passes with NuGet audit enabled.

### Non-critical batch — Group A (owner-approved subset: M7, M4, L5, L11)

Committed `fd0a30e` (verified: SIMF.Api + SIMF.Infrastructure build 0/0 with audit on; SIMF.Api.Tests 1135/1135):
- **M7 + L11** — new `SecurityHeadersMiddleware` on the API (`X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, HSTS over HTTPS) on every response.
- **M4** — `DependencyInjection.EnsureAiPromptHashSecretConfigured` boot guard (Production refuses to start on the publicly-derivable dev HMAC key); dead `AiAssistant` config block deleted.
- **L5** — OpenAI provider error body redacted (`AiAuditDetail.RedactValue`) + length-capped before logging.

### Non-critical batch — Group B progress (2026-06-20)

Each item below was verified in an **isolated git worktree pinned to a clean commit** — a concurrent worker kept the main test tree mid-migration throughout, so live-tree runs were unreliable; the worktree gives a trustworthy green.

Committed + verified:
- **M5** — JWT lib version aligned (Web `8.2.1` → `8.18.0`) — `e0dd62d` (Web tests 39/39).
- **M1** — magic-byte validation on asset image upload — `e0dd62d` (Api tests 1135/1135).
- **L10** — Website landing image-URL escaping (`cssBgImage` scheme allow-list) — `5732471` (JS `node --check` + build).
- **M2** — speaker-presentation upload PDF/Office magic-byte allow-list + tests — `af9cb35c`.
- **L3** — external-link assets restricted to https + non-internal hosts — `af9cb35c` (Api tests 1135 + presentation 7/7).
- **L1** — recording-stream token bound to the user (`sub`) + `Cache-Control: no-store` — `bea5a9df` (SessionRecording 11/11).
- **M3** — OTP/account codes hashed at rest: keyed-HMAC `AccountCodeHasher` (truncated to the frozen 16-char column, D-110-safe); all four OTP services (`SignIn`, `Registration`, `Password`, `Badge`) store the hash, email the plaintext, and hash-then-compare on verify; test helpers recover the plaintext by brute-forcing the hash. *Full verification in progress.*

- **L2** — **committed `d4a461da`**: every dynamic `perm:` policy now also requires `account_state="Approved"` (defense-in-depth), so an admin endpoint can't be reached by a non-approved account even if it forgets the explicit `RequireApprovedAccount` chain. (Admin + PublicRelations perm-holders are already Approved; gate endpoints use role policies — unaffected. Confirmed: full SIMF.Api.Tests 1143/1143.)

Closed by decision / deferred:
- **L6** — `/admin/logs` redaction: **accepted, not implemented.** Redacting emails from log downloads harms legitimate admin debugging; the logs are verified clean of secrets and the endpoint is already permission-gated (`Logs.View`) + audited. Control = keep `Logs.View` narrowly assigned (current state).
- **M6** — CP `SessionModerationDesk` permission: **committed `0cd796b6`.** The page now carries `[RequirePermission(PermissionCatalog.Questions.Moderate)]` and the Hide/Unhide/Push buttons are wrapped in `<AuthorizedAction Permission="…Questions.Moderate">` (the code already existed + is seeded — no migration). Verified: SIMF.ControlPanel.Tests 180/180. The API's per-session moderator/Administrator enforcement is unchanged (the stronger gate).
- **L4** (shared path-traversal guard — latent, no current exploit) and CSP for Web/CP (needs report-only + live-browser tuning) — deferred.

**Group C (ops/CI/device):** INFO-1 (IIS header suppression), INFO-4 (SBOM + vulnerable-package CI gate), L7 (`AllowedHosts` — tied to held H2), L8/L9 (Flutter — device-verified, with held C2).
