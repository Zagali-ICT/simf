# Page 013 — API (الرئيسية · Home)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The rules
behind each call are in [Page_013_Logic.md](Page_013_Logic.md).

> **Status:** Home is a **router/landing screen with no data of its own**.
> It makes **one** live, best-effort call (the notification count, signed-in only).
> The **privilege** comes from the **cached auth session** (no call), and the
> **on-login bundle** `GET /app/bootstrap` is **BUILT (D-251)** on the backend but
> is **not called by the shipped app**. The Flutter screen is built (D-296) and
> was **redesigned to the KSA frames (D-378, 2026-06-13)** — the redesign added
> **no new API call**.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247)
> — so the route below is `GET /api/v1/app/account/notifications/unread-count`.

## Privilege — from the cached auth session (no call)
The app privilege (`Guest` / `Visitor` / `Staff` / `Moderator`) is **read from the
cached auth state** (`AuthStateSignedIn.session.user.appRole`, populated at
sign-in), not fetched. No session ⇒ `Guest`. There is **no endpoint** for Home to
"ask its privilege" (Logic L-1). As built the gate is binary: guest layout vs one
shared signed-in layout (Logic L-2).

## E1 — `GET /app/account/notifications/unread-count`  (unread count for the bell badge) — **EXISTS**
| | |
|---|---|
| Route | `GET /api/v1/app/account/notifications/unread-count` |
| Access | Authenticated (own `sub`); signed-in only — `Guest` does not call it. No permission code. |
| App privilege | Any signed-in role (`Visitor` / `Staff` / `Moderator`) |
| Returns | `ApiResult<UnreadCountResponse>` = `{ unreadCount }` — Home reads the count for the badge |
| Behaviour on Home | **Best-effort + non-blocking**: any error resolves to `0` → the badge hides (Logic L-5) |

```jsonc
// ApiResult<T> envelope (SIMF-API-001) — Home reads the unread count from the payload
{
  "success": true,
  "data": {
    "unreadCount": 0      // → bell Badge.count; hidden when 0 (Logic L-5)
  },
  "error": null
}
```

> **As-built (D-296, unchanged by D-378):** verified against the shipped endpoint —
> `UnreadNotificationCountEndpoint` (`Endpoints/Account/NotificationEndpoints.cs`)
> returns `SIMF.Contracts.Notifications.UnreadCountResponse` → `{ unreadCount }`.
> The Flutter bell binds via `NotificationsRepository.getUnreadCount()` →
> `unreadNotificationCountProvider` (guest → `0` with no request; `ApiFailure` →
> `0`, silent). (An earlier draft of this doc named the bare `…/notifications`
> path; the real route is `…/notifications/unread-count` — corrected here.)

## E2 — `GET /app/bootstrap`  (on-login bundle) — **BUILT (D-251), not called by the shipped app**
| | |
|---|---|
| Route | `GET /api/v1/app/bootstrap` |
| Access | **Any signed-in account** (valid bearer token); own `sub`. **Not** `RequireApprovedAccount` — a pending user may bootstrap to cache its privilege + routing decision on login. No new permission code (app self-read). |
| App privilege | Signed-in, including **pending** |
| Returns | `ApiResult<AppBootstrap>` — an additive, read-only aggregate (user + unread count + server time) |
| Status | **BUILT (D-251)** — composed from existing reads (`GetCurrentUserAsync` + `UnreadCountMineAsync`). **As built (D-378) the app does not call it**: Home reads the session cached at sign-in instead (Logic L-3) |

```jsonc
// AppBootstrap (SIMF.Contracts.Account.Bootstrap)
{
  "user": {                 // CurrentUserResponse — same wire shape as GET /app/users/me
    "id": "guid",
    "email": "string",
    "displayName": "string",
    "appRole": "Visitor",   // "Visitor" | "Staff" | "Moderator"
    "preferredLanguage": "ar",
    "registrationStatus": "Pending",  // "Pending" | "Approved" | "Rejected"
    "avatarUrl": "string?",
    "profileComplete": false          // D-374 additive field
  },
  "unreadNotificationCount": 0,            // same source as the bell unread-count
  "serverTimeUtc": "2026-06-03T10:00:00Z"  // for client clock-skew correction
}
```

> This is the **app's on-login bootstrap** aggregate, kept available for future
> use. Richer screen-specific data (full profile, agenda, …) is fetched lazily per
> screen, not bundled here. Covered by `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.

## E3 — LIVE banner — **NO API (D10)**
The signed-in LIVE banner ships **without an API (D10)**. It is rendered from
**static l10n content**; tapping it opens the **live-broadcast screen**. There is
**no request, no response shape, and no error state** for the banner.

## E4 — Social + Visit-Saudi links — **NO API (compile-time config, D-369)**
The تابعنا brand buttons and the روح السعودية row are backed by **`--dart-define`
build configuration**, not the backend: `SIMF_SOCIAL_X` / `SIMF_SOCIAL_INSTAGRAM` /
`SIMF_SOCIAL_LINKEDIN` / `SIMF_SOCIAL_YOUTUBE` / `SIMF_SOCIAL_TIKTOK` (default
empty ⇒ that button is **inert**) and `SIMF_VISIT_SAUDI_URL` (default
`https://www.visitsaudi.com`). Launching is best-effort via the shared
`launchExternalUri` helper — no request to the SIMF backend (Logic L-7).

## Summary
| Call | Route | Status | Used for |
|---|---|---|---|
| Privilege | — (cached auth session) | shipped | picking the guest vs signed-in layout (Logic L-1/L-2) |
| E1 notifications | `GET /api/v1/app/account/notifications/unread-count` | **exists** | bell unread badge (signed-in, best-effort) |
| E2 bootstrap | `GET /api/v1/app/bootstrap` | **BUILT (D-251), unused by the app** | on-login aggregate kept available (Logic L-3) |
| E3 LIVE banner | — | **no API (D10)** | static promo → live-broadcast screen |
| E4 external links | — | **no API (config, D-369)** | social + Visit-Saudi external links |

> **Net:** Home has **no required data API**. Its only live call (E1) is optional,
> signed-in-only and best-effort; everything else is cached session, static l10n,
> or compile-time config.
