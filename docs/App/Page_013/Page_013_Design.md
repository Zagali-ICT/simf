# Page 013 — Design (الرئيسية · Home)

Flutter screen design for the Home router screen (#13, `path=/`) — layout,
components, states, localization and RTL. The behaviour is in
[Page_013_Function.md](Page_013_Function.md); the rules are in
[Page_013_Logic.md](Page_013_Logic.md).

## Layout
A single scrollable landing surface:
1. **App bar** — title AR **الرئيسية** / EN **Home**, with the **notification bell +
   unread badge** trailing (leading in RTL).
2. **Live / YouTube banner** — a full-width promo card near the top (static/config,
   no API — D10). Tapping it opens the live/stream view.
3. **Entry tiles** — the home navigation tiles, **gated by app privilege**
   (Logic L-2). Laid out as a grid/list of cards.
4. **Sign-in prompt** — shown for `Guest` only (Logic L-2), inviting sign-in.

## Components
| Component | Role | Notes |
|---|---|---|
| App bar | Title + bell | Bell trailing (LTR) / leading (RTL) |
| Notification badge | Unread count over the bell | From `GET /app/account/notifications/unread-count`; hidden when `0` / for `Guest` |
| Live banner card | Promo for the live stream | No API (D10); static/config; tap → live view |
| Entry tile card | Navigation tile | One per allowed destination; privilege-gated |
| Sign-in prompt | Guest affordance | Tap → sign-in flow; `Guest` only |

## Data binding
- **Privilege** ← JWT claim (no fetch). Drives which tiles/prompt render (Logic L-1/L-2).
- **Unread count** ← `GET /app/account/notifications/unread-count` (best-effort; Logic L-5).
- **Tiles / banner** ← static/config for now ("no data for now"); the on-login bundle
  (`GET /app/bootstrap`, **TO BUILD, D9**) will back cached content later (Logic L-3).

## States
| State | Trigger | Visual |
|---|---|---|
| **Loading** | App routed to `/` | Tiles paint immediately from privilege; **no blocking spinner** (Home has no blocking fetch). Bell shows without a count while the count call is in flight |
| **Empty** | "No data for now" | **Normal** state — the entry tiles ARE the content; no empty-state placeholder |
| **Error** | Notification count call failed | Bell shows **no count**; **no blocking error UI** (silent, Logic L-5) |
| **Success** | Count returned | Bell shows the unread badge (hidden when `0`); tiles for the privilege shown |
| **Guest** | No token | Sign-in prompt shown; badge + gated tiles hidden (Logic L-2) |

## Localization
- Title: AR **الرئيسية** · EN **Home**. All tile/prompt labels resolve from the app's
  localization resources (no hard-coded strings).

## RTL
- In Arabic the whole screen **mirrors**: app bar direction, the bell's side, the tile
  grid flow, and the banner. Layout uses direction-aware widgets so LTR/RTL both lay
  out correctly. Numerals in the badge follow the app's locale convention.

## Accessibility
- The bell carries an accessible label that includes the unread count when present.
- Entry tiles and the sign-in prompt are reachable and labelled for screen readers.
- Tap targets meet the minimum touch-size guidance.
