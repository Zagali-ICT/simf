# Entry badge — بطاقة الدخول (Page 032, `#32`)

- **Route:** `/badge` (`RouteNames.badge`, bottom-nav tab). Access: **Signed-in** (badge issued on approval).
- **Figma:** **758:1469** ("QR" — per the screen doc). **Clean-code freeze:** D-633 (2026-07-04).
- **Node inconsistency FLAGGED (not resolved):** the class doc cites `758:1469` while `badge_screen_test.dart`'s group name cites `221:769`. Same screen, two frame ids in the codebase — surfaced for the owner (as with session_moderate D-622), not guessed.

## Purpose

The visitor's entry badge: on approval the My-Area dashboard
(`GET /app/account/dashboard`, `RequireApprovedAccount`) supplies the identity and
the QR encodes the opaque `qrId`. The gold-bordered white card holds the QR + the
"امسح للدخول" hint + the identity strip (avatar, name, tier, the masked
`ID · ••••` reference), with role-based actions below. The strip is **tinted by the
profile type's colour** (`identity.pageColor` ← `ProfileType.PageColor`, gold
fallback) so each tier's badge is distinct (D-763). Not-approved / pending /
load-failure keep their respective states.

**True-guest state (BUG-016 sibling — BUG-013, 2026-07-26).** The five bottom-nav
tabs switch **inside** `SimfAppShell`'s IndexedStack, so no go_router navigation
happens and the router's auth gate on route 32 never runs: a visitor with **no
account at all** lands on this screen. It used to render the not-approved copy
("your account is not approved yet…"), describing a registration the guest never
submitted, with no way out. A signed-out user now gets the shared
`SimfGuestPrompt` — `badgeGuestBody` ("sign in or create an account to get your
entry badge") plus **Sign in** / **Create account** actions. The pending copy is
unchanged for genuinely pending accounts.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `badge_screen.dart` (158) | `BadgeScreen` + State — the approval-gated dashboard load, the loading / not-approved / error / pending / badge dispatch (`_buildBody`), and the top-level `maskedBadgeId` helper (kept here — the badge test unit-tests it). The badge case composes the card + actions. |
| `widgets/badge_qr_card.dart` (`BadgeQrCard`) | The gold-bordered white card — the **standard square** QR (`QrImageView`, D-743; the round D-423 style was undecodable by the in-app ZXing scanner), the scan hint, and the identity strip (`SimfAvatar` + name + tier + `ID · {maskedId}`) tinted by `identity.pageColor` via `parseHexColor` (`core/utils/hex_color.dart`), gold fallback + luminance-based ink (D-763). |
| `widgets/badge_actions.dart` (`BadgeActions`) | The role-based actions (D-426) — a visitor's gold "امسح لإضافة شخص" (→ the fullscreen `ScanContactScreen`) + outlined "share my contact"; an exhibitor's "scan visitor" — with the shared `_actionButton` filled/outlined helper. **DEF-EXH-005:** the branch takes the signed-in `AppRole` (`CurrentUser.effectiveAppRole`), not the dashboard's `identity.isVisitor` flag — `isVisitor` is false for EVERY partner type, so Staff / Moderator / Media / Sponsor were all shown the exhibitor-only scan button and the router (`_routeRoles[106] = {exhibitor}`) bounced them. Staff / Moderator / Guest now get no action button. |

The off-states already used the shared `SimfEmptyState`/`SimfErrorState` (kept).
Screen was already fully tokenised (no raw `Color(0x..)`). `maskedBadgeId` stays in
the screen (the test imports it); the card receives the pre-masked string. Every
file ≤400 lines.

## L4 Figma parity

No golden — the QR + the three-provider auth/dashboard/reference setup make a
golden heavy. The **12 badge widget tests** are the render baseline: they drive the
QR (`find.byType(QrImageView)`), the identity strip (name/tier/`ID · •••• C123`),
the **strip tint** (pageColor → server colour + gold fallback, D-763), the role
actions, and the not-approved / pending / error states. (Same no-golden call as
venue_map D-615 / session_moderate D-622.)

## Level-F

Wired: QR renders the `qrId`; visitor → scan-contact (fullscreen modal) +
share-my-contact; exhibitor → scan-visitor; retry re-fetches the dashboard;
not-approved / pending states gate the QR. Reads `GET /app/account/dashboard` +
`referenceNumberProvider`. No missing API.

## Tests

`test/features/badge/badge_screen_test.dart` (QR + identity render, strip tint +
gold fallback, `maskedBadgeId` unit test, Arabic name, not-approved, pending,
error+retry, role actions) and `test/core/utils/hex_color_test.dart` (the
`parseHexColor` parser). E2E: `docs/tests/e2e/mobile-badge.md`.

## Related decisions

- **D-763** (identity strip tinted by the profile type's server colour `ProfileType.PageColor` via `identity.pageColor` + `parseHexColor`; gold fallback + luminance-based ink; supersedes the Page_014 "pageColor carried but unused" note).
- **D-633** (this clean-code freeze — `_Badge` split into `BadgeQrCard` + `BadgeActions`; node inconsistency flagged).
- **D-320** (screen built), **D-423** (circle QR — visual style superseded by D-743), **D-426** (role-based actions).
- **DEF-EXH-005** (the actions gate on the signed-in app ROLE instead of the dashboard `isVisitor` flag, killing the dead exhibitor-scan control shown to Staff / Moderator / Media / Sponsor).
- **D-743** (badge QR → standard **square** so the in-app `flutter_zxing` scanner can decode it; the round style read on a phone camera but not in-app. Same change fixed gate / exhibitor / contact scanners via the shared `SimfScannerBody` full-frame `cropPercent`).

## Logo / photo boxes (owner 2026-07-26)

Every logo / photo box on this page renders through the shared
[`SimfLogoImage`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_logo_image.dart):
a brand mark FITS its box (`BoxFit.contain`, replacing the crop-happy
`BoxFit.cover`), a portrait still fills its frame (`BoxFit.cover`), and — where
the box is not inside a tappable row — pressing it opens the picture full size
in [`SimfImageViewer`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_image_viewer.dart)
(pinch-zoom, named for a screen reader, close / back to dismiss). The rules and
their scenarios live once in [`e2e/mobile-logo-viewer.md`](../../../tests/e2e/mobile-logo-viewer.md)
(E2E-LOGO-001..008).
