# Page 032 — بطاقة الدخول · Entry badge

Per-page documentation folder (App screen 32).

## Identity
| | |
|---|---|
| Mockup page | **32** (`Mockup.html`) |
| Route | `RouteNames.badge` → `/badge` (**auth-gated**) |
| Titles | AR **بطاقة الدخول** · EN **Entry badge** |
| Section | 6 — Badge & notifications |
| Nature | **The visitor's QR entry badge** — a scannable QR (the account `qrId`) + the visitor's name |
| App privilege | **Signed-in (Approved).** The dashboard read is `RequireApprovedAccount`; the route is in `_authenticatedRoutes`. |
| Status | API **BUILT** (reuse — `GET /app/account/dashboard`, D-300); **Flutter screen BUILT** |

## API (authoritative contract)
Reuses the shipped My-Area dashboard read (no new API) — the screen consumes
**only** the identity from it:
- `GET /api/v1/app/account/dashboard` → `ApiResult<MyAreaDashboard>` (`RequireApprovedAccount`).
  Used fields: `identity.qrId` (string?, null until the account is Approved),
  `identity.fullNameEn` / `identity.fullNameAr`.

The Flutter layer reuses the My-Area data layer
(`features/myarea/data/myarea_repository.dart` `getDashboard()` +
`myarea_models.dart` `MyAreaDashboard` / `MyAreaIdentity`) — the same wire
contract; no duplicate model or repository.

## Behaviour
On open the screen loads the dashboard and reads the identity. When
`identity.qrId` is non-empty it renders a centred `QrImageView`
(`qr_flutter`, `version: QrVersions.auto`, `size: 240`, `gapless: true`)
inside a white card, with the visitor's localized name below and a
"show this at entry" hint. When `qrId` is null/empty (a pending account whose
badge is not issued yet — Page_014 L-1) it shows the pending state
("your badge is available after approval"). Loading / error+retry are the
standard surfaces. The QR encodes the opaque `qrId` only. UI is interim (final
visuals from SIMF-VID-001).

## Tests
- Widget: `src/Mobile/simf_app/test/features/badge/badge_screen_test.dart`
  (issued QR + name, null-qrId pending state, error→retry, Arabic).
- API: covered by the My-Area dashboard tests (`tests/SIMF.Api.Tests`, the
  shipped `account/dashboard` read).
- E2E: [`docs/tests/e2e/mobile-badge.md`](../../tests/e2e/mobile-badge.md).
