# Page 012 — وضع الضيف · Guest mode

Per-page documentation folder (App screen 12).

## Identity
| | |
|---|---|
| Mockup page | **12** (`Mockup.html`) |
| Route | `RouteNames.guestMode` → `/guest` (**public, anonymous**) |
| Titles | AR **وضع الضيف** · EN **Guest mode** |
| Section | 1 — Start & entry |
| Nature | **Informational entry screen** — explains guest browsing vs. account-only features; two actions |
| App privilege | **Public (anonymous).** No token, no API call. |
| Status | **No API** (informational); **Flutter screen BUILT** |

## API (authoritative contract)
**None.** The screen renders entirely client-side, so it is offline-safe — there
is no read or write. It is the explanatory bridge between the entry choices and
the public app: from here a guest either continues into the app (home) or goes to
sign-in.

## Behaviour
A centred informational layout: an explore icon, a headline (**Browsing as
guest**), and two body lines — what a guest **can** do (browse the sessions,
speakers, venue map and media) and what **needs an account** (the smart badge,
personal notifications and booking). A primary **Continue as guest** action
(`context.go('/')` → home) and a secondary **Sign in** action
(`context.pushNamed(RouteNames.signIn)` → Page_003). Bilingual (AR/EN). UI is
interim (final visuals from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/guest/guest_mode_screen_test.dart`
  (renders headline + both actions; primary → home; secondary → sign-in).
- API: none (informational screen — no endpoint).
- E2E: [`docs/tests/e2e/mobile-guest-mode.md`](../../tests/e2e/mobile-guest-mode.md).
