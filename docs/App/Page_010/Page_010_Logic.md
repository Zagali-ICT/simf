# Page 010 — Logic (تم التسجيل بنجاح · Registration success)

Business rules and behaviour for the registration-success confirmation. The
backend contract is in [Page_010_API.md](Page_010_API.md).

> Last updated: 2026-06-13 — conformance pass to the as-built code (D-366 / D-369 / D-373).

## Nature
This is a **transitional confirmation** screen. It owns **no operation** of its
own — the profile was already created by the Page 007-01 save, and the screen
issues **zero API calls** (it is a `StatelessWidget`). Its job is to display the
*pending-approval* result + the issued registration reference and route the
user onward.

## State model
The just-registered account is in a **pending-approval** state
(`registrationStatus` not yet `Approved`). The lifecycle the user sits inside here:

```
Page 007-01 save  ──success──▶  [Pending approval]  ──admin approves──▶  [Approved]
       │                              │                                      │
profile created +              Page 010 shown                     full app access
referenceNumber issued      (reference via extra)            (watched on Page 011)
```

## Rules
| # | Rule |
|---|---|
| **L-1** | The screen's normal entry is a successful Page 007-01 profile save. An out-of-flow arrival (deep link / restore, signed-in) is tolerated — it renders the same screen with the mask fallback (L-3); signed-out access is bounced to `/sign-in` by the router gate. |
| **L-2** | The account is **pending**; the user is told the request is under review and a confirmation email is coming. No retry of the registration is offered from here. |
| **L-3** | **Reference number (D-373).** The save response's `referenceNumber` (`SIMF-YYYY-NNNNNNNN`, issued once at profile creation by a DB sequence) is carried here as the route **extra** and rendered on the reference card. When the extra is absent (offline / out-of-flow arrival, or a pre-D-373 save) the literal mask `SIMF-2026-xxxx` renders instead — **no fetch**, the page stays offline-safe. (This superseded the D-366 always-masked rule.) |
| **L-4** | Navigation in is a **`goNamed` replacement** — the sign-up form is removed from the back stack so the user cannot edit a submitted profile by pressing back. The header chevron pops when a stack exists, otherwise goes home. |
| **L-5** | Both buttons are pure client navigation, offline-safe: gold **حالة التسجيل** → Page 011; outlined **الانتقال للرئيسية** → home. There is **no status poll on this screen** — Page 011 owns the `GET /app/users/me` status read. |
| **L-6** | **Contact tiles (D-369).** Each tile is gated on its compile-time config value: `BuildConfig.supportPhone` / `supportEmail` (`--dart-define` `SIMF_SUPPORT_PHONE` / `SIMF_SUPPORT_EMAIL`). Empty value (the current default) → `onTap` null, the tile is **inert** — never a dead intent. Non-empty → launch `tel:` / `mailto:` via the shared `launchExternalUri` helper (`url_launcher`), **best-effort**: a missing handler or failed launch is swallowed and the user stays on the page. |

## Client logic
- On entry: render the static confirmation (header, mark, headline, copy,
  reference card, buttons, tiles, footer). No fetch — the only dynamic input is
  the `String?` route extra (`referenceNumber ?? 'SIMF-2026-xxxx'`).
- Reference value and the chevron glyph are pinned `TextDirection.ltr` so the
  code never reverses under the Arabic locale.
- Primary button → `goNamed(registrationStatus)`; secondary → `go('/')`;
  chevron → `pop()` if `canPop()`, else `go('/')` (all client-only).
- Tile taps fire `launchExternalUri(Uri(scheme: 'tel'|'mailto', path: …))`
  unawaited; failures are silently ignored.

## Server logic
- None owned by this screen. The profile + `referenceNumber` were persisted by
  the Page 007-01 save (`POST /app/account/user-profile`, D-373); approval is an
  **admin-side** action elsewhere (Control Panel); the status read used later
  belongs to Page 011 (`GET /app/users/me`, D-249).

## Validation
- No input fields → no field validation on this screen.

## Error / empty / RTL handling
| Case | Behaviour |
|---|---|
| **Base render** | Always succeeds — static content, no network dependency. |
| **No reference extra / offline arrival** | The card shows the `SIMF-2026-xxxx` mask (L-3); nothing blocks, nothing fetches. |
| **Tile launch fails / no handler** | Swallowed (best-effort, L-6) — the user simply stays on the page; the screen never crashes. |
| **Empty** | Not applicable — there is no list/data to be empty. |
| **RTL** | Arabic locale renders the centred column natively; the reference value + chevron glyph stay LTR. Both AR and EN strings are first-class (`AppL10n`). |

## Dependencies
- **Page 007-01** (interests step / single profile save) — the normal entry
  point and the source of the `referenceNumber` route extra (D-373).
- **Page 011** (registrationStatus) — the primary forward navigation target and
  the owner of the status read; **Home screen** — the secondary target.
- **`BuildConfig.supportPhone` / `supportEmail`** + **`launchExternalUri`**
  (`url_launcher`) — the contact tiles (D-369); empty config keeps them inert
  (the official values are still an open owner input on the redesign board).
