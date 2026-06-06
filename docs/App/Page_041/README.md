# Page 041 — المزيد · More

Per-page documentation folder (App screen 41).

## Identity
| | |
|---|---|
| Mockup page | **41** (`Mockup.html`) |
| Route | `RouteNames.more` → `/more` (**public, anonymous**) |
| Titles | AR **المزيد** · EN **More** |
| Section | 8 — Settings & legal |
| Nature | **Navigation hub** — a list of tiles routing to the secondary screens; a static app-version line at the bottom |
| App privilege | **Guest+ (anonymous).** No API; pure in-app navigation. |
| Status | **No API** (navigation hub); **Flutter screen BUILT** |

## API (authoritative contract)
None. The screen makes **no network calls** — it is a list of `ListTile`s that
`context.pushNamed` to already-built routes.

## Behaviour
A `ListView` of tiles (leading icon + title + trailing chevron). Each tile
routes to:

| Tile | Route |
|------|-------|
| About the forum | `RouteNames.aboutForum` → `/about` |
| Accessibility | `RouteNames.accessibility` → `/settings/accessibility` |
| Terms & conditions | `RouteNames.terms` → `/terms` |
| Rate | `RouteNames.rate` → `/rate` |
| Notifications | `RouteNames.notifications` → `/notifications` |
| Media partners | `RouteNames.mediaPartners` → `/media-partners` |

A static `SIMF v0.1.0` version line is centred at the bottom. The auth gate on
the **destination** routes (e.g. Notifications #33, Rate #40) still applies —
tapping while signed-out bounces to sign-in (router redirect). UI is interim
(final visuals from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/more/more_screen_test.dart`
  (renders the tiles, version line, tap About → navigates).
- E2E: [`docs/tests/e2e/mobile-more.md`](../../tests/e2e/mobile-more.md).
