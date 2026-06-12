# SIMF Wave-1 Production-Readiness Validation — 2026-06-12

Owner mandate (D-371): prove the Wave-1 entry/auth flow end-to-end by **real
testing** — live backend, real database verification after every write, and a
real browser drive from onboarding to home — after building the C1–C7
constraint set. **Gate rule: all green → Wave 1 declared ready → Wave 2
begins.**

**Result: ALL PASS** — with one **real defect found and fixed during the run**
(D-372, the missing-bearer-token circular wiring; see §5). Environment: local
API `:5175` (Development, FaceDetection enabled), SQL Server `.`
(`SIMF_Identity` + `SIMF_App`), Flutter **release** web build `:8080`, Chrome
(devtools-driven, semantics-enabled).

## 1. Constraint builds (B1–B4) — shipped same-day, all gates green

| # | Constraint | Commit | Tests |
|---|-----------|--------|-------|
| C4 | Phone standard — Saudi `05XXXXXXXX`/`+9665XXXXXXXX`, intl E.164, client+server in lockstep (also the walk-in desk validator) | `adb35ec` | 12 new server theories; client `phone_validation_test` |
| C5 | Visitor type locked to **"Normal" (عادي)** (seeder rename General→Normal; server rejects other audience tiers; app hides the picker, Other requires a pick) | `9bddc09` | 3 server + 2 widget tests |
| C6 | Plate number — additive `UserProfiles.PlateNumber` (migration `App/D371_AddUserProfilePlateNumber`, nvarchar(7)), 3 letters + 1–4 digits ≤7 chars, stored normalized upper-cased, optional | `e6e9576` | 12 server theories incl. stored-value round-trip; client `plate_validation_test` |
| C7 | Male-mandatory **camera-only** photo + dual-layer human-face detection (on-device ML Kit + server FaceAiSharp SCRFD ONNX, offline/NCA; 400 `VISITOR_ID_IMAGE_NO_FACE`) | `14ea384` | real-model `UserProfileFaceGateTests`; male-gate widget tests |

Suite states after B4: API **1007/1007**, Flutter **346/346** (347 after the
D-372 regression test), `flutter analyze` clean, API build 0 warnings/0 errors.

## 2. Layer 1 — live API + DB chain (fresh account `e2e-w1-0612a@example.sa`)

| Step | Action | Result | DB evidence |
|------|--------|--------|-------------|
| R1 | sign-up | **201** generic | `AspNetUsers` row (`EmailConfirmed=0`, `Registered`, `Visitor`); `AccountCodes` `EmailVerification`, unconsumed |
| R2 | re-sign-up same email | **201 identical** (enumeration-resistant, D-198) | still exactly 1 user row |
| R4 | verify wrong code | **400** | code not consumed |
| R3 | verify real DB code | **200** | `EmailConfirmed=1`, state `EmailVerified` |
| R5 | sign-in | tokens issued (2FA off) | — |
| R6 | `GET /users/me` | `Visitor` / `Pending` | matches row |
| R7 | lookups | countries 57 · visitor types **Normal+VIP** · interests 4 · organisations 5 | — |
| R8 | full profile save (Normal type, `050 123-4567`, plate `abj 1234`, Aramco, male, DOB 1990-05-15, Luhn ID) | **200** | **field-by-field match** — plate stored **`ABJ1234`** (normalized), type **Normal**, 2 interest links |
| R9 | negatives: empty interests / bad Luhn / under-18 / bad plate `AB1234` / bad mobile `0401234567` / **VIP self-pick** | **all 400** | no partial writes — C4/C5/C6 enforced live |
| R10a | id-image: faceless PNG | **400 `VISITOR_ID_IMAGE_NO_FACE`** (bilingual, audited) | — |
| R10b | id-image: real face | **200** (the positive ONNX path proven live) | `IdImageRelativePath` stored |
| R11 | refresh | rotated; **old-token reuse → 401** (reuse-detection kill) | — |
| R12 | sign-out | 200; signed-out refresh token → **401** | — |

## 3. Layer 1 — login, forgot/reset, 2FA

- Wrong password → **401**; the **per-email rate limiter** tripped live (429)
  during rapid attempts — hardening verified incidentally.
- forgot-password → 200; `AccountCodes` `PasswordReset` row → reset-password →
  **password hash changed in DB**; old password **401**, new password →
  tokens.
- 2FA (flag flipped on the throwaway account, pre-approved): sign-in returned
  the **OTP challenge** (no tokens) → DB code → `verify-otp` → tokens. Flag
  reset afterwards.

## 4. Layer 2/3 — admin approval + the full UI browser drive

- **A3 (lifecycle close):** admin signed in on the Cp audience with the real
  **TOTP** second factor → `POST /admin/visitors/{id}/approve` → **200** → DB
  `Approved` → visitor re-sign-in shows `Approved` and a **minted QR id**;
  plate + face image round-trip on the profile read.
- **A4 (UI drive, release web build, second fresh user
  `e2e-w1-ui@example.sa`):** registration form (KSA design, D-370) → real 201
  → **OTP screen** → DB code typed → verified → sign-in → **routed to the
  profile form** (D-288) → form driven end-to-end: **no type picker under
  Visitor (C5)**, Saudi switch → national-ID branch (C1), standard phone, the
  **plate field (C6)**, DOB picker capped at today−18 (D-197), Aramco via the
  live typeahead → interests (2/10) → single save → **success screen** →
  **home as a signed-in visitor** (unread badge 3, avatar, visitor tiles).
  Screenshots: `docs/screenshots/e2e-w1-ui-registration-success.png`,
  `docs/screenshots/e2e-w1-ui-home-signed-in.png`. DB check: **every
  UI-entered value persisted exactly** (incl. `PlateNumber=ABJ1234`,
  `ProfileType=Normal`, Gender=2, 2 interest links).

## 5. Defect found by this run — FIXED (D-372)

The first UI sign-in landed on a guest-looking home: `POST sign-in` 200 but
every authenticated follow-up (`/users/me`, profile, unread-count) went out
**without the Authorization header** → 401. Root cause: the
`authTokenSourceProvider` override resolved
`authControllerProvider.notifier` **eagerly inside the provider graph**,
forming a circular dependency (controller → repository → api client → token
source → controller); Riverpod's recovery left the dio interceptors holding a
**never-initialised controller**, so `currentAccessToken()` was always null.
Not web-specific — the same wiring ships in the native app. **Fix:** the new
`AuthTokenBridge` (simf_auth_pkg) — a passive object with no provider
dependencies that `AuthController.build()` registers itself into; deterministic
regression test `test/app/auth_token_wiring_test.dart` (fails with the old
wiring, passes with the bridge). Re-driven live after the fix: the full
authenticated chain works (§4).

## 6. Honest notes & rig caveats

- The chrome-devtools MCP browser crash-loops on the **debug** (DDC) build;
  the run switched to the **release** build, which drove stably. One re-drive
  was needed after a cached pre-fix bundle (hard reload fixed it).
- The earlier R11 "401" was this runner's own token-capture error (file
  round-trip); re-run in-session passed — recorded for transparency.
- The **camera capture + on-device ML Kit** check cannot run on web; it is
  enforced by code + widget tests, and the **server face gate** (the
  authority) was proven live both ways (R10). A native-emulator camera drive
  remains the one outstanding physical-device check.
- The backend `dotnet run` background process died quietly several times
  (~15–30 min lifetime) during the session — a dev-rig artifact (restart +
  `/health` before each phase); no production relevance.
- Owner wording "17 char" for the plate was implemented as **7** (3 letters +
  max 4 digits — the only consistent total); flagged in D-371 for correction.

## 7. Verdict

**Wave 1 is production-ready by this run's evidence**: every flow from
onboarding to home passed live with DB verification, every C1–C7 constraint is
enforced at both client and server, and the one real defect found was fixed,
regression-tested, and re-proven live. **Wave 2 may begin.**
