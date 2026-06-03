# Page 001 — Logic (البداية · Splash)

Boot/bootstrap rules behind the screen. The order below is the launch sequence.

## L-1 Boot sequence (order)
On cold launch the splash runs these steps, then routes out:
1. Show logo + start a **minimum display timer** (so the splash never flickers).
2. **Store-update check** (L-2) — store-native, non-blocking unless a hard update is required.
3. **First-run vs resume** probe from the local DB (L-3).
4. **Load stored session** from secure storage (L-4).
5. **Route out** to the last saved screen or the correct entry screen (L-5).

Steps that can run in parallel (store check + local DB read) should; routing waits for all
required results and for the minimum display timer.

## L-2 Store-update check (store-native — NOT a SIMF API)
The latest-version lookup is done through the **native app store** mechanism
(Play Store / App Store in-app-update APIs), **not** a SIMF backend endpoint.
- **Hard / forced update** → show a **non-dismissible** prompt; the only action opens the
  store listing. The app does not route into its screens until updated.
- **Soft update** → show a **dismissible** prompt ("Later"); on dismiss, continue L-3.
- **Up to date** or **store check unreachable** → skip silently and continue (never block boot).

> There is no SIMF version endpoint. Do not add one; the store is the source of truth.

## L-3 First-run vs resume (local DB)
Read the local DB / preferences flag that marks whether the app has completed first launch.
- **First run** (flag absent) → there is no stored session and no saved last screen; route to
  onboarding / sign-in entry (Guest entry). Set the first-run-done flag after the user passes entry.
- **Resume** (flag present) → proceed to load the stored session (L-4) and the last saved screen (L-5).

The local DB is the offline-allowed store per the mobile architecture; reading it here is a
local read only — no network.

## L-4 Load stored session
Load the persisted session/tokens from **secure** local storage.
- **No stored session** → treat as signed-out; destination is the signed-out entry (L-5).
- **Stored session present** → attempt a **silent refresh** via `POST /app/auth/refresh`
  (Page_001_API E1) to obtain fresh tokens, then resolve identity via
  `GET /app/account/profile` (Page_001_API E2) to derive the app privilege.
  - Refresh **succeeds** → signed-in; carry the refreshed tokens + privilege to the destination.
  - Refresh **fails** (expired/revoked refresh token, 401) → clear the stored session and route
    to the signed-out entry; do not loop.
  - Refresh **network error** (offline / server unreachable) → keep the stored session and route
    using the **cached** identity to the last saved screen in a degraded/offline state (let the
    destination screen surface its own retry). Never strand the user on the splash.

## L-5 Route-out (last saved screen)
After L-2–L-4 resolve and the minimum display timer has elapsed, navigate **once** (replace,
so back does not return to the splash):
- **Signed-in + valid session** → open the **last saved screen** persisted in the local DB
  (the screen the user was on when the app was last backgrounded/closed); if none is recorded,
  fall back to the privilege's home (Home, Page 12/13 per the mobile flow).
- **First run / signed-out / refresh failed** → onboarding / sign-in entry.
- **Hard update required** → never routes out (blocked on the update prompt, L-2).

App privilege (Guest / Visitor / Moderator / Staff) is decided from the loaded session +
profile, then handed to the destination — the splash itself has **no** privilege gate; it runs
before privilege is known.

## L-6 Edge cases & fallbacks
- Store check unreachable → continue as up-to-date.
- Local DB read fails / corrupt → treat as first run (safest), do not crash.
- Refresh times out → offline-degraded resume on cached identity (L-4).
- Refresh returns 401 → clear session, go to entry.
- Minimum display timer ensures no sub-100 ms flash; a hard cap ensures the splash always
  advances even if a step hangs (timeout each network step; on timeout, take its fallback).
- The screen is non-interactive aside from the update dialog — no user input is collected here.

## L-7 Dependencies
- **`POST /app/auth/refresh`** — shipped; silent session refresh (Page_001_API E1).
- **`GET /app/account/profile`** — shipped; identity + roles to derive privilege (Page_001_API E2).
- **Store-native in-app-update API** — platform SDK, not SIMF; no backend dependency.
- **Local DB** — the app's offline store for the first-run flag, last-saved-screen, and cached
  identity (mobile-architecture local-DB allowance).

## L-8 Localization
Arabic primary (RTL), English secondary. The splash carries no body copy; any update-dialog
text is bilingual (AR primary). Direction follows the active locale; the centered logo is
direction-neutral.
