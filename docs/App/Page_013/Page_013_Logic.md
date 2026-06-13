# Page 013 — Logic (الرئيسية · Home)

Client + server logic, state transitions, privilege gating, validation, and
error/empty/RTL handling for the Home router screen (#13, `path=/`). The user-facing
steps are in [Page_013_Function.md](Page_013_Function.md); the contracts are in
[Page_013_API.md](Page_013_API.md).

## L-1 — Privilege is read from the cached auth state
The app privilege (`Guest` / `Visitor` / `Staff` / `Moderator`) is **not fetched per
screen**. As built, Home watches `authControllerProvider` and reads
`AuthStateSignedIn.session.user.appRole` — the session cached at sign-in. No
session (signed-out) ⇒ **`Guest`**. Home never calls the server to "ask what
privilege am I". (App privilege enum: `Guest=0, Visitor=1, Moderator=2, Staff=3`.)

## L-2 — Privilege picks one of two layouts (D-378)
As built the gate is **binary**: `Guest` gets the guest layout; **every signed-in
role gets the same signed-in layout** (frame 203:1236 — there are no per-role tile
differences on Home).
| Privilege | Layout | Sign-in CTA | Notification badge | Tiles |
|---|---|---|---|---|
| `Guest` | Guest (frame 512:1492) | **shown** (gold button) | never requested | 2×2 public tiles + the **locked بطاقتي** tile (visible, disabled, inert) + FAQ/discover rows |
| `Visitor` / `Staff` / `Moderator` | Signed-in (frame 203:1236) | hidden | shown (best-effort) | LIVE banner + the three tile sections + تابعنا + اكتشف |

> The locked بطاقتي tile is a **visual cue only** — it renders on the disabled
> palette with no tap handler. Hiding/locking a tile is display gating, not a
> security control (see Validation).

## L-3 — On-login session cache; bootstrap endpoint unused
The session (user identity + privilege) is cached by the auth flow at **sign-in**;
Home (and the other screens) read that cache rather than re-fetching. The backend
on-login bundle `GET /app/bootstrap` is **BUILT (D-251)** — but the shipped app
does **not call it**: Home needs nothing beyond the cached sign-in session plus
the best-effort unread count (L-5), so the aggregate stays an available,
additive read for future use.

## L-4 — No blocking data on Home
Home renders **without any blocking network call**. The only live call is the unread
notification count (L-5), which is **non-blocking and best-effort**. The LIVE
banner (L-6) renders from static config and makes **no call**; the social /
Visit-Saudi links (L-7) are compile-time config and make **no call**.

## L-5 — Notification unread count (best-effort)
The bell badge binds to `unreadNotificationCountProvider`
(`FutureProvider.autoDispose<int>`), which calls
`NotificationsRepository.getUnreadCount()` →
`GET /app/account/notifications/unread-count` → `{ unreadCount }`. Rules:
- Only attempted when **signed in** (`AuthStateSignedIn`); a guest resolves to `0`
  without any request.
- **Non-blocking:** Home renders fully before/independent of this call; while in
  flight (or on any non-data state) the screen uses `0`.
- **Silent failure:** any `ApiFailure` resolves to `0` — the badge simply hides
  (`Badge.count` with `isLabelVisible: unread > 0`); Home never shows a blocking
  error for the badge.
- The provider **recomputes when the auth state changes**.

## L-6 — LIVE banner (no API, D10)
The LIVE promo banner ships **without an API (D10)**. It renders static l10n
content («مباشر» / «الجلسة الافتتاحية تُبث الآن» / «شاهد البث المباشر»). Tapping it
opens the **live-broadcast screen**. No request, no response shape, no error state
tied to a fetch.

## L-7 — Config-driven external links (D-369 contract)
The تابعنا brand buttons and the روح السعودية row open compile-time configured
URLs (`BuildConfig`: `SIMF_SOCIAL_X` / `SIMF_SOCIAL_INSTAGRAM` /
`SIMF_SOCIAL_LINKEDIN` / `SIMF_SOCIAL_YOUTUBE` / `SIMF_SOCIAL_TIKTOK`, and
`SIMF_VISIT_SAUDI_URL` defaulting to `https://www.visitsaudi.com`). Rules:
- An **empty configured value keeps that button inert** (`onTap: null`) — never a
  dead intent.
- Launching goes through the shared `launchExternalUri` helper
  (`lib/core/external_link.dart`): **best-effort** — a missing handler or a
  malformed URI is swallowed and the user simply stays on the page.
- Social links open with `LaunchMode.externalApplication`.

## State transitions
| State | Cause | UI |
|---|---|---|
| `render-guest` | No signed-in session | Guest layout paints immediately (no spinner); the count endpoint is never called |
| `render-signed-in` | Cached session exists (any role) | Greeting layout paints immediately from the cached name + privilege |
| `badge-loading` | Count call in flight (signed-in only) | Bell shows with the badge hidden (`unread = 0`) |
| `badge-loaded` | Count returned | `Badge.count` shows the number (hidden when `0`) |
| `badge-error` | Count call failed (`ApiFailure`) | Resolves to `0` → badge hidden; no error UI (L-5) |
| `auth-change` | Sign-in / sign-out while on `/` | The watched auth state flips the layout; the count provider recomputes |

## Validation
Home takes **no user input** and submits **no form**, so there is no field
validation. The only "validation" is the privilege gate (L-1/L-2): the client trusts
the cached session for **display gating only** — every protected destination screen
and every backend endpoint still enforces its own authorization server-side. Hiding
or locking a tile is **not** a security control.

## Error / empty / RTL handling
- **Empty:** Home's tiles are static navigation — there is no empty-state
  placeholder; the tiles are the content.
- **Error:** the only fallible call is the notification badge, which fails
  **silently** (L-5); external-link launches fail silently too (L-7). Nothing on
  Home raises a blocking error.
- **RTL:** the body mirrors with the ambient direction (tiles, list rows, the
  greeting header). The guest header chrome stays **forced LTR** (circled back
  chevron at the physical left — the D-363 pattern); both AR/EN labels resolve
  from localization (`الرئيسية • ضيف` / `Home • Guest`).

## Dependencies
- **Cached auth session** for privilege + display name (L-1) — shipped.
- `GET /app/account/notifications/unread-count` for the unread count (L-5) — **exists**.
- `GET /app/bootstrap` on-login bundle (L-3) — **BUILT (D-251), not called by the shipped app**.
- LIVE banner (L-6) — **no API (D10)**; tap target = the live-broadcast screen.
- Social / Visit-Saudi dart-defines (L-7) — **config, inert while unset (D-369)**.
- A dedicated app FAQ endpoint/screen — **does not exist yet**; the guest FAQ row
  opens the About page (tracked follow-up, D-378).
