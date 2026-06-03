# Page 013 — API (الرئيسية · Home)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The rules
behind each call are in [Page_013_Logic.md](Page_013_Logic.md).

> **Status:** Home is a **router/landing screen with no data of its own for now**.
> It makes **one** live, best-effort call (the notification count). Two further items
> are noted: the **privilege** comes from the **JWT claim** (no call), and the
> **on-login bundle** is **(TO BUILD)** this wave.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247)
> — so the route below is `GET /api/v1/app/account/notifications`.

## Privilege — from the JWT claim (no call)
The app privilege (`Guest` / `Visitor` / `Staff` / `Moderator`) is **read from the
JWT claim**, not fetched. No token ⇒ `Guest`. There is **no endpoint** for Home to
"ask its privilege" — the client decodes the cached token (Logic L-1).

## E1 — `GET /app/account/notifications`  (unread count for the bell badge) — **EXISTS**
| | |
|---|---|
| Route | `GET /api/v1/app/account/notifications` |
| Access | Approved account (`RequireApprovedAccount`); own `sub`. Signed-in only — `Guest` does not call it. No new permission code. |
| App privilege | Visitor and above |
| Returns | `ApiResult<…>` — Home consumes the **unread count** for the badge |
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

> Verify the exact payload field name / shape against the shipped endpoint before
> binding — this page only needs the **unread count**, not the list. If the count is
> exposed under a different field, bind to that; the Home contract is "give me the
> unread number, best-effort".

## E2 — `GET /app/bootstrap`  (on-login bundle: all data + privileges) — **(TO BUILD, in-progress, D9)**
| | |
|---|---|
| Route | `GET /api/v1/app/bootstrap` |
| Access | Approved account (`RequireApprovedAccount`); own `sub` |
| App privilege | Visitor and above |
| Returns | `ApiResult<AppBootstrap>` — an **additive, read-only aggregate** the app fetches **once on login** and caches (Logic L-3) |
| Status | **(TO BUILD, in-progress, D9)** — additive aggregate over existing reads; no schema/enum change, no migration |

```jsonc
// AppBootstrap (TO BUILD, D9) — indicative shape; confirm on build
{
  "privilege": "Visitor",   // mirrors the JWT claim for convenience
  "data": { }               // the cached bundle of app data fetched once on login
}
```

> This is **not a Home-only call** — it is the **app's on-login bootstrap** that Home
> (and other screens) read from cache. Listed here because Home depends on the cached
> result. Mark every binding against it as provisional until D9 ships.

## E3 — Live / YouTube banner — **NO API (D10)**
The live/YouTube promo banner ships **without an API for now (D10)**. It is rendered
from **static / config-driven** content; tapping it opens the live/stream view. There
is **no request, no response shape, and no error state** for the banner.

## Summary
| Call | Route | Status | Used for |
|---|---|---|---|
| Privilege | — (JWT claim) | shipped | gating tiles/actions (Logic L-1/L-2) |
| E1 notifications | `GET /api/v1/app/account/notifications` | **exists** | bell unread badge (best-effort) |
| E2 bootstrap | `GET /api/v1/app/bootstrap` | **(TO BUILD, D9)** | on-login all-data + privileges cache |
| E3 live banner | — | **no API (D10)** | live/YouTube promo |

> **Net:** Home itself has **no required data API for now**. Its only live call (E1)
> is optional and best-effort; everything else is JWT-claim, cache, or static.
