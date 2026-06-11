# Page 013 — Function (الرئيسية · Home)

What the user does on this screen, step by step, and the privilege/auth gate that
shapes it. Visual + state detail is in [Page_013_Design.md](Page_013_Design.md);
the rules behind each decision are in [Page_013_Logic.md](Page_013_Logic.md).

## Purpose
Home is the app's **router/landing screen (#13, `path=/`)**. It is the surface the
user lands on after boot. It **requires no login** and carries **no data of its own
for now** — its content is shaped entirely by the **app privilege**
(`Guest`/`Visitor`/`Staff`/`Moderator`), which is read from the **JWT claim**.

## Privilege / auth gate
| | |
|---|---|
| Login required to open | **No** — Home opens for everyone, including `Guest` (signed-out). |
| Privilege source | **JWT claim** (`privilege` / role claim). No token ⇒ `Guest`. |
| What the privilege controls | Which entry tiles / actions are shown or hidden (see Logic L-2). |
| Data fetched by this screen | **None for now.** The only live call is the unread-notification count (badge). |
| On-login caching | After a successful sign-in the app fetches **all data + privileges once** and caches them; Home then reads from that cache. The aggregate fetch is `GET /app/bootstrap` — **(TO BUILD, in-progress, D9)**. |

## Elements
| Element | Description | Privilege | Action |
|---|---|---|---|
| App bar / title | AR **الرئيسية** / EN **Home** | All | — |
| Notification bell + unread badge | Bell icon with unread count | All (count is `0` / hidden for `Guest`) | Tap → notifications screen; count from `GET /app/account/notifications/unread-count` |
| Live / YouTube banner | Promo banner for the live stream | All | Tap → opens the live/stream view. **No API for now (D10)** — static/config-driven |
| Entry tiles (navigation) | The home navigation tiles | Gated per privilege (Logic L-2) | Tap → routes to the matching screen |
| Sign-in prompt (Guest only) | "Sign in" affordance for `Guest` | `Guest` only | Tap → sign-in flow |

## User steps
1. App boots and routes to Home (`/`). No blocking network call is required to render.
2. The app determines the **privilege from the JWT claim** (no token ⇒ `Guest`).
3. Home renders the entry tiles/actions **allowed for that privilege** (Logic L-2).
4. In the background the app reads the **unread-notification count**
   (`GET /app/account/notifications/unread-count`) and paints the bell badge. A failure here is
   silent — the badge simply shows no count (Logic L-5).
5. The **live/YouTube banner** renders from static/config (no API for now, D10).
6. The user taps a tile / the bell / the banner / the sign-in prompt and navigates on.

## Navigation
- **From:** boot/splash, or any "home" tab/back-to-home action.
- **To:** notifications, the live/stream view, the sign-in flow (Guest), and each
  entry tile's destination screen.

## Acceptance criteria
- Home opens with **no login** for every privilege, including `Guest`.
- The visible tiles/actions match the **JWT privilege** exactly (Logic L-2).
- The notification bell shows the **unread count** when signed in; it degrades
  silently to no-count on error or for `Guest`.
- The screen renders correctly with **no data** (nothing on Home blocks on a fetch).
- Full **RTL** in Arabic; both AR/EN labels resolve from localization.
