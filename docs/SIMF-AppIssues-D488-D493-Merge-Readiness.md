# SIMF — App-Issues + NCA-Security Batch — Merge-Readiness & Go-Live Prerequisites

Last updated: 2026-06-22

This is the handover artefact for merging the app-issues-report + NCA-security
batch to `main`. It exists because the merge and the production deploy are
**owner-gated manual steps** (no `az`/`gh`/PAT on the build machine, and the
merge must not land until the production environment carries two secrets — see
§3). Read §3 before clicking merge.

The facts below are grounded in the repository at the verification commit, not
asserted from memory: env-var names, boot-guards and error text were read from
the source on 2026-06-22.

---

## 1. Scope — what is in this PR

- **PR branch:** `feature/app-issues-report-d488-492` → `main`
- **Verification tip:** `fb107ac1`
- **Merge-base with `main`:** `fcbe12f3`
- Both `feature/app-cp-api-split` and the PR branch are at `fb107ac1` on origin.

The PR is the shared integration branch's 12-commit delta over `main`. It mixes
this batch's app + security work with the concurrent worker's CP Phase-0 / AI
commits (all already committed on the shared branch, all heading to `main`):

| Commit | Theme |
|--------|-------|
| `cfe49564` | ci: provision SQL Server LocalDB before the test gate |
| `1db674b1` | **D-488** app issues-report batch — 6-digit OTP gate, sign-in email validation, app-wide error i18n, notifications mark-on-open + live unread counter |
| `6103b05a` | **D-489** "Keep me logged in" off → session in-memory only, gone on restart |
| `c386ff3d` | **D-490** resend the visitor 2FA OTP in place — new `resend-otp` endpoint |
| `046d5351` | **D-491** Gemini provider + hybrid centralized AI; live subtitle feeds session-summary |
| `e5ef07df` | **D-492** D-110 freeze-lift governance for NCA Wave-6 Identity schema |
| `f86f9b4c`, `d99c4e77`, `6ab0bc5d` | CP Phase-0 — AiPrompts SimfSelect + hosting/residency-risk + history drill-downs |
| `669e9f88` | **D-493** NCA 6e admin-lockout (Critical) + avatar AV-scan bypass (High) |
| `854405b3` | CP Phase-0 review nits |
| `fb107ac1` | admin media-gallery AV-scan (A6-18 — completes the D-493 flag) |

> #7a biometric-enable OTP step-up (**D-486**, `1f2d0d9b`) is **already in `main`**
> (it predates the merge-base) — it is not part of this PR.

---

## 2. Verification evidence (at `fb107ac1`, 2026-06-22)

- **Backend** — `dotnet test tests/SIMF.Api.Tests` → **Passed: 1190, Failed: 0**
  (run on the CP-independent test project so the concurrent worker's in-flight CP
  edits cannot colour the result).
- **Flutter app** — `flutter test` (`src/Mobile/simf_app`) → **All tests passed!
  (555)**.
- The three security fixes each carry a regression test: avatar-EICAR-rejected,
  media-image-EICAR-rejected, dormancy-never-disables-admin.

---

## 3. 🔴 Go-live prerequisites — BLOCKING (set BEFORE the merge deploys)

The API **fail-fasts at boot** if either secret is absent. Both are blank in the
committed config (`appsettings.json` ships empty placeholders by design). Set
them as environment variables in the production host **before** the merge
triggers a deploy, or the live API will not start.

| Secret (env var) | Config key | Boot guard (verified) |
|------------------|-----------|------------------------|
| `SIMF_Jwt__SigningKey` | `Jwt:SigningKey` | `Program.cs:277` throws `"Jwt:SigningKey must be configured and at least 32 bytes long."` if `< 32` bytes — **every** environment. |
| `SIMF_Storage__UserIdDocumentEncryptionKey` | `Storage:UserIdDocumentEncryptionKey` | `EnsurePiiEncryptionConfigured` throws at boot **in Production** if it is not a valid base64 32-byte key (NCA A2-10 — encrypts the UserProfile PII columns at rest; Wave 6a made this mandatory). |

- The env prefix is `SIMF_` (`Program.cs:36` — `AddEnvironmentVariables("SIMF_")`),
  and `__` maps to the config-section separator.
- The encryption key must be **base64 of exactly 32 bytes**.
- Deploy runs from `main` (the pipeline does not deploy feature branches), so the
  crash window opens the moment the merge lands — not before.

---

## 4. Pre-handover obligation

- **Re-instate the D-110 freeze** before the production publish / handover. The
  freeze was lifted in waves for the build push; the persistence + enum contract
  surface must be frozen again at handover (per `CLAUDE.md` and the DECISIONS_LOG
  freeze sections).

---

## 5. 🟠 Flagged NCA follow-ups — NOT in this PR, owner decisions before go-live

These were surfaced by the D-493 review and **deliberately not fixed
unilaterally** because each is a judgement/deploy call, not a clear-and-safe
edit. Both are currently latent (not live holes); resolve before relying on the
feature in production.

1. **Dormancy NULL-baseline mass-disable risk** —
   `DormantAccountService.cs:40` keys dormancy on
   `(user.LastSuccessfulSignInAtUtc ?? user.CreatedAt) < cutoff`. The
   `LastSuccessfulSignInAtUtc` column (Wave 6d) is NULL for every existing user
   until their next sign-in, so the **first** sweep after deploy would fall back
   to `CreatedAt` and disable every long-standing Approved non-admin at once.
   - Mitigated today: the sweep is **default-off** (`DormantAccountDisableDays <= 0`
     returns 0) and **admins are protected** (`UserType != Admin`).
   - Decision needed before enabling: backfill `LastSuccessfulSignInAtUtc = deploy-time`
     for existing approved users (data migration → **D-110 owner approval**), or
     change the NULL semantics to "never dormant until a real sign-in is recorded."

2. **Wave-7 upload scanner is an EICAR-only stub and fails open** — the default
   `IUploadScanner` detects only the EICAR test signature and, on a scan error,
   allows the upload. The scan **seam** is now wired on every untrusted path
   (ID-document, asset, speaker presentation, avatar, admin media), so flipping to
   a real engine is a config/DI change with full coverage already in place.
   - Decision needed before go-live: wire a real engine (e.g. ClamAV / cloud AV)
     and switch the policy to **fail-closed**. Fail-closed only changes behaviour
     once a real engine exists (the stub never errors), so do both together.

---

## 6. Owner-only manual steps (access this environment does not have)

1. **Open the PR** `feature/app-issues-report-d488-492` → `main` in Azure DevOps
   (no `az`/`gh`/PAT locally to create it programmatically).
2. **Set the two production env vars** from §3 before the merge deploys.
3. **#7a biometric-enable on-device test** — exercise the emailed-OTP step-up →
   Face-ID enrol flow on a device with an enrolled fingerprint and a live mailbox.
   `FLAG_SECURE` (NCA A11-6) blocks in-app screen capture, so this is a manual
   visual check, not an automated screenshot.
