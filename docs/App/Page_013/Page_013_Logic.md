# Page 013 — Logic (الرئيسية · Home)

Client + server logic, state transitions, privilege gating, validation, and
error/empty/RTL handling for the Home router screen (#13, `path=/`). The user-facing
steps are in [Page_013_Function.md](Page_013_Function.md); the contracts are in
[Page_013_API.md](Page_013_API.md).

## L-1 — Privilege is read from the JWT claim
The app privilege (`Guest` / `Visitor` / `Staff` / `Moderator`) is **not fetched per
screen**. It is read from the **JWT claim** held by the client. No token (signed-out)
⇒ **`Guest`**. Home never calls the server to "ask what privilege am I" — it decodes
the cached token. (App privilege enum: `Guest=0, Visitor=1, Moderator=2, Staff=3`.)

## L-2 — Privilege gates which entry tiles/actions appear
Home shows or hides its entry tiles/actions per privilege:
| Privilege | Sign-in prompt | Notification badge | Gated tiles |
|---|---|---|---|
| `Guest` | **shown** | hidden / `0` | public tiles only |
| `Visitor` | hidden | shown | visitor tiles |
| `Staff` | hidden | shown | staff/gate tiles |
| `Moderator` | hidden | shown | moderator tiles |

> The exact per-privilege tile list is owner-driven and **not finalized for now**
> (Home has "no data for now"). The gating mechanism is fixed (JWT claim); the
> catalogue of tiles is pending the owner's home layout. Treat this table as the
> gating contract, not the final tile inventory.

## L-3 — On-login bundle fetch + cache (D9)
On a **successful sign-in**, the app performs a **one-time** fetch of **all data +
privileges** and caches them locally; Home (and the other screens) then read from
that cache rather than re-fetching. The aggregate fetch is the on-login bootstrap
bundle `GET /app/bootstrap` — **(TO BUILD, in-progress, D9)**, an additive read-only
aggregate. Until it ships, the app uses its existing per-screen reads and the JWT
privilege; Home itself stays data-free.

## L-4 — No blocking data on Home
Home renders **without any blocking network call**. The only live call is the unread
notification count (L-5), which is **non-blocking and best-effort**. The live/YouTube
banner (L-6) renders from static/config and makes **no call**.

## L-5 — Notification unread count (best-effort)
The bell badge calls `GET /app/account/notifications` (exists) for the unread count.
Rules:
- Only attempted when **signed in** (a token exists); `Guest` shows no count.
- **Non-blocking:** Home renders fully before/independent of this call.
- **Silent failure:** on any error (network/401/500) the badge simply shows **no
  count** — Home never shows a blocking error for the badge.

## L-6 — Live / YouTube banner (no API, D10)
The live/YouTube promo banner ships **without an API for now (D10)**. It is rendered
from static/config-driven content. Tapping it opens the live/stream view. No request,
no response shape, no error state tied to a fetch.

## State transitions
| State | Cause | UI |
|---|---|---|
| `render` | App routed to `/`; privilege decoded from JWT | Tiles for that privilege paint immediately (no spinner needed) |
| `badge-loading` | Notification count call in flight (signed-in only) | Bell shows without a count |
| `badge-loaded` | Count returned | Bell shows unread count (hidden when `0`) |
| `badge-error` | Count call failed | Bell shows **no count**; no error UI (L-5) |
| `guest` | No token | Sign-in prompt shown; badge + gated tiles hidden (L-2) |

## Validation
Home takes **no user input** and submits **no form**, so there is no field
validation. The only "validation" is the privilege gate (L-1/L-2): the client trusts
the JWT claim for **display gating only** — every protected destination screen and
every backend endpoint still enforces its own authorization server-side. Hiding a
tile is **not** a security control.

## Error / empty / RTL handling
- **Empty:** "no data for now" is the **normal** Home state — it is not an error and
  shows no empty-state placeholder; the entry tiles are the content.
- **Error:** the only fallible call is the notification badge, which fails **silently**
  (L-5). Nothing on Home raises a blocking error.
- **RTL:** in Arabic the whole screen mirrors (tiles, app bar, bell position); both
  AR/EN labels resolve from localization (`الرئيسية` / `Home`).

## Dependencies
- **JWT claim** for privilege (L-1) — shipped.
- `GET /app/account/notifications` for the unread count (L-5) — **exists**.
- `GET /app/bootstrap` on-login bundle (L-3) — **(TO BUILD, in-progress, D9)**.
- Live/YouTube banner (L-6) — **no API for now (D10)**.
- Final per-privilege tile catalogue (L-2) — **pending owner** ("no data for now").
