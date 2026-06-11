# Page 013 — API (الرئيسية · Home)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The rules
behind each call are in [Page_013_Logic.md](Page_013_Logic.md).

> **Status:** Home is a **router/landing screen with no data of its own for now**.
> It makes **one** live, best-effort call (the notification count). The **privilege**
> comes from the **JWT claim** (no call), and the **on-login bundle**
> `GET /app/bootstrap` is now **BUILT (D-251)**. The **Flutter screen is built**
> (D-296).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247)
> — so the route below is `GET /api/v1/app/account/notifications/unread-count`.

## Privilege — from the JWT claim (no call)
The app privilege (`Guest` / `Visitor` / `Staff` / `Moderator`) is **read from the
JWT claim**, not fetched. No token ⇒ `Guest`. There is **no endpoint** for Home to
"ask its privilege" — the client decodes the cached token (Logic L-1).

## E1 — `GET /app/account/notifications/unread-count`  (unread count for the bell badge) — **EXISTS**
| | |
|---|---|
| Route | `GET /api/v1/app/account/notifications/unread-count` |
| Access | Authenticated (own `sub`); signed-in only — `Guest` does not call it. No permission code. |
| App privilege | Visitor and above |
| Returns | `ApiResult<UnreadCountResponse>` = `{ unreadCount }` — Home reads the count for the badge |
| Behaviour on Home | **Best-effort + non-blocking**: on any error the badge shows **no count** (Logic L-5) |

```jsonc
// ApiResult<T> envelope (SIMF-API-001) — Home reads the unread count from the payload
{
  "success": true,
  "data": {
    "unreadCount": 0      // → bell badge; hidden when 0 (Logic L-5)
    // (list items omitted here — Home uses the count only)
  },
  "error": null
}
```

> **As-built (D-296):** verified against the shipped endpoint —
> `SIMF.Contracts.Notifications.UnreadCountResponse` → `{ unreadCount }`. The Flutter
> bell binds to that field via `NotificationsRepository.getUnreadCount()`. (An earlier
> draft of this doc named the bare `…/notifications` path; the real route is
> `…/notifications/unread-count` — corrected here.)

## E2 — `GET /app/bootstrap`  (on-login bundle) — **BUILT (D-251)**
| | |
|---|---|
| Route | `GET /api/v1/app/bootstrap` |
| Access | **Any signed-in account** (valid bearer token); own `sub`. **Not** `RequireApprovedAccount` — a pending user must bootstrap to cache its privilege + routing decision on login. No new permission code (app self-read). |
| App privilege | Signed-in, including **pending** |
| Returns | `ApiResult<AppBootstrap>` — an additive, read-only aggregate the app fetches **once on login** and caches (Logic L-3) |
| Status | **BUILT (D-251)** — composed from existing reads (`GetCurrentUserAsync` + `UnreadCountMineAsync`); no schema/enum change, no migration |

```jsonc
// AppBootstrap
{
  "user": {                 // the same wire shape as GET /app/users/me (Page_011)
    "id": "guid",
    "email": "string",
    "displayName": "string",
    "appRole": "Visitor",   // "Visitor" | "Staff" | "Moderator"
    "preferredLanguage": "ar",
    "registrationStatus": "Pending",  // "Pending" | "Approved" | "Rejected"
    "avatarUrl": "string?"
  },
  "unreadNotificationCount": 0,            // same source as the bell unread-count
  "serverTimeUtc": "2026-06-03T10:00:00Z"  // for client clock-skew correction
}
```

> This is the **app's on-login bootstrap** — Home (and other screens) read the cached
> result. Richer screen-specific data (full profile, agenda, …) is fetched lazily per
> screen, not bundled here. Covered by `tests/SIMF.Api.Tests/AppBootstrapTests.cs`.

## E3 — Live / YouTube banner — **NO API (D10)**
The live/YouTube promo banner ships **without an API for now (D10)**. It is rendered
from **static / config-driven** content; tapping it opens the live/stream view. There
is **no request, no response shape, and no error state** for the banner.

## Summary
| Call | Route | Status | Used for |
|---|---|---|---|
| Privilege | — (JWT claim) | shipped | gating tiles/actions (Logic L-1/L-2) |
| E1 notifications | `GET /api/v1/app/account/notifications/unread-count` | **exists** | bell unread badge (best-effort) |
| E2 bootstrap | `GET /api/v1/app/bootstrap` | **BUILT (D-251)** | on-login user + unread + server-time cache |
| E3 live banner | — | **no API (D10)** | live/YouTube promo |

> **Net:** Home itself has **no required data API for now**. Its only live call (E1)
> is optional and best-effort; everything else is JWT-claim, cache, or static.
