# Page 001 — Function (البداية · Splash)

Functional specification: what the page does on launch. Logic rules are in
[Page_001_Logic.md](Page_001_Logic.md); the backend contract is in
[Page_001_API.md](Page_001_API.md); the visual design is in
[Page_001_Design.md](Page_001_Design.md). Last updated 2026-07-10 (D-736 — the update
check now reads the SIMF version policy).

## Purpose
The **splash / bootstrap** screen — the first screen on every cold launch. It shows the
SIMF brand lock-up (logo + tagline + forum name + edition/date) while the app performs
its startup work: check the server version policy for a newer/mandatory version,
restore any stored session, and then route the user to the right destination — the
**last saved screen** for a signed-in user, or the correct entry screen for their
state. It is **not** interactive — the user takes no action here (apart from the
conditional update dialog).

## Actors
- **Anyone launching the app** — no privilege is known yet. The screen runs before
  sign-in state is resolved; privilege (Guest / Visitor / Moderator / Staff) is decided
  here from the restored session and carried in the auth state the destination reads.

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Brand lock-up | Centred logo + tagline + forum name + edition/date, held for the duration of the boot work (1200 ms minimum display time). |
| FE-2 | Update check | Reads the **SIMF version policy** (`GET /app/version-policy`, D-736 — the CP-configured per-platform min/latest version + store URL) and compares against the installed version with semver; **fail-open** on any error — Logic L-2. |
| FE-3 | First-run flag | Reads the `onboardingCompleted` preferences flag to tell a first launch from a returning one (consulted on the signed-out branch) — Logic L-3. |
| FE-4 | Session restore | Waits for the auth cold-start restore: secure-storage tokens → silent refresh (only if the access token is missing/expired) → identity read — Logic L-4. |
| FE-5 | Route-out | One replace-navigation to the profile form (incomplete profile), the last saved screen, Home, the OTP step, sign-in, or onboarding — Logic L-5. |

## User actions & navigation
| Action | Result |
|--------|--------|
| (none — no controls) | The screen is non-interactive; it auto-advances when boot work completes. |
| Update dialog → تحديث الآن / Update now | Opens the **admin-configured store listing URL** for this platform (D-736; a dialog is only ever shown when a usable URL exists — the anti-brick rule). |
| Update dialog → لاحقاً / Later (soft update only) | Dismisses, **snoozes this version for 3 days** (D-736) and continues the normal route-out (a soft dialog continues — and snoozes — however it is closed). |
| Boot completes — signed in, profile incomplete | Routes to the profile form (Page 007, D-374 gate). |
| Boot completes — signed in, profile complete | Routes to the **last saved screen** from preferences (resumable content routes only), else Home. |
| Boot completes — awaiting email-OTP | Routes to the OTP entry screen. |
| Boot completes — first run (onboarding not completed) | Routes to onboarding. |
| Boot completes — signed out / no session / refresh failed | Routes to the sign-in entry. |

## Acceptance criteria (functional)
- AC-1 On every cold launch the lock-up is shown while boot work runs (1200 ms minimum display time honoured).
- AC-2 The update check reads the **SIMF version policy** (`GET /app/version-policy`, D-736) and **fails open** — an unreachable/broken policy never blocks boot.
- AC-3 A **forced** (hard) update blocks progress behind a non-dismissible update prompt (only after a live successful policy fetch and only with a usable store URL); a soft update is dismissible, snoozed per version, and never blocks the route-out.
- AC-4 First-run launches (onboarding flag unset) route to onboarding; returning signed-out launches route to sign-in.
- AC-5 A stored session is restored silently — a valid cached access token resumes directly; an expired one is refreshed via `POST /app/auth/refresh` — and the authoritative identity is read from `GET /app/users/me` before routing to a signed-in destination.
- AC-6 With a valid session and a complete profile the app opens the **last saved screen** (or Home); an incomplete profile is routed to the profile form first (D-374).
- AC-7 The screen never blocks indefinitely — every step is time-boxed (5 s policy check, 8 s auth wait, 4 s storage reads) with a defined fallback route (Logic L-6).
