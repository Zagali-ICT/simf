# Page 032 — بطاقة الدخول · Entry badge

Per-page documentation folder (App screen 32).

Last updated: 2026-06-13 — KSA Wave-2 redesign (D-378, frame 221:769, commit `f35ffe3`).

## Identity
| | |
|---|---|
| Mockup page | **32** (`Mockup.html`) |
| Route | `RouteNames.badge` → `/badge` (**auth-gated**) |
| Titles | AR **بطاقة الدخول** · EN **Entry badge** |
| Section | 6 — Badge & notifications |
| Nature | **The visitor's QR entry badge** — a scannable QR (the account `qrId`) inside a gold-bordered card, a gold identity strip (avatar · name · tier · masked id tail), and an add-a-contact scanner action |
| App privilege | **Signed-in (Approved).** The dashboard read is `RequireApprovedAccount`; the route is in `_authenticatedRoutes`. |
| Status | API **BUILT** (reuse — `GET /app/account/dashboard`, D-300); **Flutter screen BUILT, redesigned to KSA Wave-2 frame 221:769 (D-378)** — gold-bordered white QR card + gold identity strip (masked id tail) + امسح لإضافة شخص → `/contacts/scan`; old screen parked in `_legacy_mockup/` |

## API (authoritative contract)
Reuses the shipped My-Area dashboard read (no new API) — the screen consumes
**only** the identity from it:
- `GET /api/v1/app/account/dashboard` → `ApiResult<MyAreaDashboard>` (`RequireApprovedAccount`).
  Used fields: `identity.qrId` (string?, null until the account is Approved),
  `identity.fullNameEn` / `identity.fullNameAr` (localized name, Arabic-primary
  fallback), `identity.avatarUrl` (identity-strip avatar, initials fallback),
  and `identity.tierNameEn` / `identity.tierNameAr` (tier line, omitted when no
  ProfileType is assigned).

The Flutter layer reuses the My-Area data layer
(`features/myarea/data/myarea_repository.dart` `getDashboard()` +
`myarea_models.dart` `MyAreaDashboard` / `MyAreaIdentity`) — the same wire
contract; no duplicate model or repository.

## Behaviour
The screen composes the shared KSA shell (`KsaPage`, title **بطاقة الدخول**,
back = pop-or-home, decorative sweep on) with `SimfTab.badge` active — i.e. the
bottom nav's raised **gold QR centre action** is the highlighted tab. On open it
loads the dashboard and reads the identity. When `identity.qrId` is non-empty it
renders (KSA frame 221:769):

- A **gold-bordered white card** (`SimfTokens.accent` border, width 1.5,
  `radiusLg`) containing a centred `QrImageView` (`qr_flutter`,
  `version: QrVersions.auto`, `size: 230`, `gapless: true`) over the caption
  **امسح للدخول** · *Scan to enter* (`badgeScanToEnter`).
- Inside the same card, a **gold identity strip** (`SimfTokens.accent`
  background): `KsaAvatar` (size 56, photo with initials fallback), the
  localized name (bold, single line, ellipsis), the localized tier line when a
  ProfileType is assigned, and the reference line `ID · <masked qrId>` rendered
  **LTR**. Mask rule (`maskedBadgeId`): a `qrId` of ≤ 4 characters is shown
  as-is; otherwise `•••• ` + the last 4 characters — the strip never exposes
  the full scan value as text.
- Below the card, an **outlined امسح لإضافة شخص** · *Scan to add a contact*
  button (`badgeAddPerson`, full-width, beige border, QR-scanner icon) that
  pushes the existing contact-QR scanner route `/contacts/scan`
  (`RouteNames.scanContact`, FDS-014).

When `qrId` is null/empty (a pending account whose badge is not issued yet —
Page_014 L-1) it shows the standard pending/empty state (`KsaEmptyState`,
`qr_code_2_outlined` icon, "ستتوفر بطاقتك بعد اعتماد حسابك." ·
"Your badge is available once your account is approved."). Loading is a centred
spinner; a load failure (`ApiFailure`) shows the standard error + retry surface
(`KsaErrorState`, "تعذّر تحميل بطاقتك." + إعادة المحاولة, retry re-fetches).
The QR encodes the opaque `qrId` only.

## Tests
- Widget: `src/Mobile/simf_app/test/features/badge/badge_screen_test.dart`
  (issued QR + identity strip + masked tail, add-person → contact scanner,
  null-qrId pending state, error→retry, Arabic, `maskedBadgeId` unit cases).
- API: covered by the My-Area dashboard tests (`tests/SIMF.Api.Tests`, the
  shipped `account/dashboard` read).
- E2E: [`docs/tests/e2e/mobile-badge.md`](../../tests/e2e/mobile-badge.md).
