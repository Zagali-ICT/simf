# Page 001 — Logic (البداية · Splash)

Boot/bootstrap rules behind the screen. The order below is the launch sequence, as built
in `lib/features/splash/splash_controller.dart` (+ the auth cold-start restore in
`simf_auth_pkg`'s `AuthController`). Last updated 2026-06-13 (conformance pass on the
D-361 as-built; the redesign changed visuals only — this boot logic is unchanged).

## L-1 Boot sequence (order)
On cold launch the splash runs these steps, then routes out:
1. Show the brand lock-up + start the **minimum display timer** — **1200 ms**
   (`minSplashDurationProvider`; overridable to zero in tests) so the splash never flickers.
2. Kick off the **auth cold-start restore** (touching `authControllerProvider` starts it; L-4).
3. **Store-update check** (L-2) — store-native; runs **concurrently** with the timer,
   capped at **5 s** (on timeout it resolves as up-to-date).
4. If the check reports a **forced** update → stop on the non-dismissible prompt (L-2).
5. Otherwise **wait for the auth restore to resolve** (leave `AuthStateInitial`), capped
   at **8 s** — on timeout the splash routes out on whatever state is current (L-6).
6. **Route out** (L-5), showing the dismissible soft-update prompt first when the check
   reported an optional update.

## L-2 Store-update check (store-native — NOT a SIMF API)
The latest-version lookup is done through the **native app store** mechanism
(Play Store / App Store in-app-update APIs), **not** a SIMF backend endpoint.
- **Hard / forced update** → show a **non-dismissible** prompt; the only action opens the
  store listing. The app does not route into its screens until updated.
- **Soft update** → show a **dismissible** prompt ("لاحقاً / Later"); the splash routes out
  **however** the dialog was closed (Later, scrim, or after "تحديث الآن / Update now") so
  the user is never stranded (L-6).
- **Up to date** or **store check unreachable/slow** (5 s cap, and `check()` must never
  throw) → skip silently and continue (never block boot).

> There is no SIMF version endpoint. Do not add one; the store is the source of truth.
> As-built the active checker is the pre-launch **`NoopAppUpdateChecker`** (always
> up-to-date; `openStoreListing()` is a no-op). The store-plugin implementation is wired
> at store-submission time by overriding `appUpdateCheckerProvider`.

## L-3 First-run vs returning launch (preferences flag)
The first-run signal is the **`StorageKeys.onboardingCompleted`** boolean in the app's
preferences storage (`simf.prefs.onboarding_completed`), set by the Onboarding screen
(Page 002) when the user completes/skips it. The splash consults it **only on the
signed-out branch** of route-out (L-5):
- **Flag absent/false** (first run) → route to **onboarding**.
- **Flag true** (returning, signed out) → route to the **sign-in** entry.

This is a local preferences read only — no network. (There is no separate "probe" step;
the flag is read inside the route-out decision.)

## L-4 Cold-start session restore (auth controller)
The restore runs in `AuthController._restoreFromStorage()` — the splash only waits for
its outcome (state leaving `AuthStateInitial`).
- Read four values from **secure** storage concurrently — access token, refresh token,
  access-token expiry (ISO), cached user JSON — under a **4 s** cap (D-295; a hung
  keystore must never stall the restore).
- **No stored refresh token** → `AuthStateSignedOut`; no network call.
- **Fast path** — the cached access token is still valid (unexpired) and a cached user
  exists → restore the session immediately (`AuthStateSignedIn`), then re-read the
  authoritative identity via `GET /app/users/me` (Page_001_API E2) **best-effort**: a
  failure is swallowed and the cached identity kept. **No refresh call** on this path.
- **Access token missing/expired** → **silent refresh** via `POST /app/auth/refresh`
  (Page_001_API E1):
  - Refresh **succeeds** → persist + sign in, then hydrate the real app-role +
    registration status + `profileComplete` from `GET /app/users/me` (the refresh
    payload's `user` carries only id/email/displayName — without the read the
    privilege would default to Guest).
  - Refresh **fails with an auth error** (expired/revoked/invalid token, or any
    non-network server error) → clear the stored session → `AuthStateSignedOut`; do not loop.
  - Refresh fails with a **network error** (offline / server unreachable / timeout,
    `NetworkUnavailable`) → resume on the **cached** session in a degraded/offline state;
    with no cached identity there is nothing to resume to → `AuthStateSignedOut`.
- **Any unexpected failure** (e.g. corrupt keystore, storage timeout) →
  `AuthStateSignedOut` — never strand the user on the splash.

A sign-in that was interrupted at the email-OTP second factor is **not** persisted; the
restore never yields `AuthStateAwaitingOtp` — that state only arises from a live sign-in
in the same run (the splash still handles it, L-5).

## L-5 Route-out
After L-1..L-4 resolve, the splash navigates **once** (a one-shot `_handled` guard;
replace navigation via `context.go`/`goNamed`, so back does not return to the splash):
- **Signed in + profile incomplete** (`profileComplete == false` from E2) → the
  **profile form** (`signUpVisitor`, Page 007) — the add-profile-first gate also applies
  to the cold-start restore (D-374).
- **Signed in + profile complete** → the **last saved location** from preferences
  (`StorageKeys.lastRoute`), if one is recorded and it `isResumableLocation` (only
  signed-in **content** routes; `/splash`, `/onboarding`, `/sign-in`, `/sign-up`,
  `/terms`, `/registration`, `/guest` and `/auth/*` are never resumed —
  `lib/app/route_resume.dart`); otherwise **Home** (`RouteNames.home`, mockup screen 13, path `/`).
- **Awaiting the email-OTP second factor** (`AuthStateAwaitingOtp`) → the OTP entry
  screen (`verifyOtp`).
- **Signed out** → **sign-in** when onboarding is completed, else **onboarding** (L-3).
- **Hard update required** → never routes out (blocked on the update prompt, L-2).

`StorageKeys.lastRoute` is written by the **router** redirect as the signed-in user
navigates (only resumable content locations are recorded); the splash owns the read.
The router also holds **protected** routes on `/splash` while the restore is still
`AuthStateInitial` (D-295) and sends protected routes to `/sign-in` when signed out.

App privilege (Guest / Visitor / Moderator / Staff) is decided from the restored session
(`appRole` from E2; Guest is the app-side fallback) — the splash itself has **no**
privilege gate; it runs before privilege is known.

## L-6 Edge cases & fallbacks (as-built caps)
- Store check unreachable / slower than **5 s** → continue as up-to-date.
- Secure-storage reads slower than **4 s** or throwing → treated as signed-out, no crash.
- Corrupt cached-user JSON → ignored (no cached identity); the restore continues.
- Auth restore not resolved within **8 s** → the splash routes out on whatever auth state
  is current (still-`Initial` falls through to the signed-out branch).
- Refresh network error → offline-degraded resume on the cached identity (L-4).
- Refresh auth error → clear session, go to the signed-out entry.
- The **1200 ms** minimum display timer prevents a flash; the caps above guarantee the
  splash always advances — every network step is time-boxed with a defined fallback.
- The soft-update dialog routes out **however** it is closed; the screen is otherwise
  non-interactive — no user input is collected here.

## L-7 Dependencies
- **`POST /app/auth/refresh`** — shipped; silent session refresh (Page_001_API E1);
  called only when the cached access token is missing/expired.
- **`GET /app/users/me`** — shipped (D-249, `profileComplete` added D-374); identity +
  `appRole` + registration status + profile-completeness to derive privilege and the
  add-profile gate (Page_001_API E2). The token payload's `AuthUser` omits these.
- **`AppUpdateChecker`** (`appUpdateCheckerProvider`) — the store-native seam; the
  pre-launch default is `NoopAppUpdateChecker`. Platform SDK, not SIMF; no backend dependency.
- **Preferences storage** — `StorageKeys.onboardingCompleted` (first-run flag, L-3) and
  `StorageKeys.lastRoute` (last saved location, L-5).
- **Secure storage** — access/refresh tokens, access-token expiry, cached user JSON
  (the cold-start restore inputs, L-4).

## L-8 Localization
Arabic primary (RTL), English secondary. The splash renders the brand lock-up copy
(`splashTagline` — the EN-only constant `SAUDI · MOD · RSNF` — plus the bilingual
`splashTitle` and `splashEventLine`) and the bilingual update-dialog strings
(`updateRequiredTitle/Body`, `updateOptionalTitle/Body`, `updateNowLabel`,
`updateLaterLabel`). Direction follows the active locale; the centred column is
direction-neutral.
