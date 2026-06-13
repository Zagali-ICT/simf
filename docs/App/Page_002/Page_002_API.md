# Page 002 — API (التهيئة · Onboarding)

Backend contract for this screen.

> **This screen has NO SIMF API.** Owner decision (D-249, reaffirmed as-built D-362/D-373):
> the onboarding carousel is **first-run-only**, **client-side**, and serves all its media
> from **bundled assets** — it makes **no call to any `/api/v1/app/*` endpoint**, sends no
> auth header, and reads/writes no SIMF table. The first-run flag is a **local-prefs**
> boolean (see [Page_002_Logic.md](Page_002_Logic.md) L-1).

## Endpoints used by this screen
**None.** There is intentionally no `GET`, `POST`, `PUT`, or `DELETE` under
`/api/v1/app/*` for this page.

| Method + route | Access / policy | Request | Response | Status |
|----------------|-----------------|---------|----------|--------|
| — (no endpoint) | — | — | — | **N/A — screen has no SIMF API** |

## Media sources (not SIMF endpoints)
All media is bundled in the app package; nothing is fetched over the network:

| Asset | Source | Notes |
|-------|--------|-------|
| `assets/images/onboarding_world_map.jpg` | bundled asset | step-1 fallback photo (shown under the navy overlay until/unless the step's video decodes) |
| `assets/videos/onboard_01.mp4` | bundled asset | step-1 looping muted background video (D-373) |
| `assets/videos/onboard_02.mp4` | bundled asset | step-2 background video — currently the same hero clip as 01 (placeholder; owner replaces in place) |
| `assets/videos/onboard_03.mp4` | bundled asset | step-3 background video — currently the same hero clip as 01 (placeholder; owner replaces in place) |
| `assets/images/simf_logo.png` | bundled asset | brand mark via the shared `SimfLogo` widget |

There is no YouTube/external player on this screen (the old YouTube-intro model was
dropped — D-362). There is no SIMF `ApiResult<T>` envelope, no SIMF error code, and no
SIMF auth involved on this screen.

## Optional future remote swap (NOT wired)
If the client ever wants the panel copy / media to be swappable without an app release,
the **existing generic** public CMS read could supply it by key — without adding any new
endpoint:

| Method + route | Access / policy | Request | Response | Status |
|----------------|-----------------|---------|----------|--------|
| `GET /api/v1/app/content/{key}` | `AllowAnonymous` (public CMS read, D-173) | path `key` (e.g. `onboarding-intro`) | `ApiResult<PublicContentBlock>` per SIMF-API-001; supports `If-Modified-Since` → `304` | **EXISTS** (`src/Backend/SIMF.Api/Endpoints/Public/PublicCmsEndpoints.cs`) — **NOT consumed by this screen** |

> `GET /app/content/{key}` is referenced as the **only** seam that would avoid a new
> endpoint. It is **not consumed by this screen today** and is **out of scope** until the
> owner asks for a remote-swappable onboarding.

## Summary
- **This screen has no SIMF API.** ✅
- All media is **bundled assets** — the world-map photo + `onboard_01..03.mp4` background clips + the logo. No YouTube, no remote media.
- First-run state is **local** (`simf.prefs.onboarding_completed`), not server-side.
- The **only** possible future API is reusing the existing `GET /app/content/{key}` (built, anonymous, D-173) — optional, not wired.
