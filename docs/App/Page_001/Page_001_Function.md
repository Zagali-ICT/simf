# Page 001 — Function (البداية · Splash)

Functional specification: what the page does on launch. Logic rules are in
[Page_001_Logic.md](Page_001_Logic.md); the backend contract is in
[Page_001_API.md](Page_001_API.md); the visual design is in
[Page_001_Design.md](Page_001_Design.md).

## Purpose
The **splash / bootstrap** screen — the first screen on every cold launch. It shows the
SIMF logo while the app performs its startup work: check the app store for a newer
version, decide first-run vs resume from the local DB, load any stored session, and then
route the user straight to their **last saved screen** (or the correct entry screen for
their state). It is **not** interactive — the user takes no action here.

## Actors
- **Anyone launching the app** — no privilege is known yet. The screen runs before
  sign-in state is resolved; privilege (Guest / Visitor / Moderator / Staff) is decided
  here from the loaded session and handed to the destination screen.

## Functional elements
| # | Element | Behaviour |
|---|---------|-----------|
| FE-1 | Logo | Centered SIMF logo held for the duration of the boot work (with a minimum display time). |
| FE-2 | Store-update check | Queries the **native app store** for the latest version (store-native, **not** a SIMF API) — see Logic L-2. |
| FE-3 | First-run vs resume probe | Reads the local DB flag to tell a brand-new install from a returning launch — Logic L-3. |
| FE-4 | Session load | Loads the stored session/tokens from secure local storage, if any — Logic L-4. |
| FE-5 | Route-out | Navigates to the last saved screen (resume) or the correct entry screen (first run / signed-out) — Logic L-5. |

## User actions & navigation
| Action | Result |
|--------|--------|
| (none — no controls) | The screen is non-interactive; it auto-advances when boot work completes. |
| Update-required dialog → Update | Opens the store listing (store-native intent) so the user can update. |
| Update-required dialog → Later (soft update only) | Dismisses and continues the normal route-out. |
| Boot completes — resume | Routes to the **last saved screen** restored from the local DB. |
| Boot completes — first run / no session | Routes to onboarding / sign-in entry (Guest mode entry). |

## Acceptance criteria (functional)
- AC-1 On every cold launch the logo is shown while boot work runs (minimum display time honoured).
- AC-2 The store-update check is performed via the **native store** mechanism, never a SIMF endpoint.
- AC-3 A **forced** (hard) update blocks progress behind a non-dismissible update prompt; a soft update is dismissible.
- AC-4 First-run launches route to onboarding / entry; returning launches resume.
- AC-5 A valid stored session is loaded and refreshed before routing to a signed-in destination.
- AC-6 With a valid session the app opens the **last saved screen**; without one it opens the signed-out entry.
- AC-7 The screen never blocks indefinitely — every failure path has a defined fallback route (Logic L-6).
