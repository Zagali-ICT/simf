# Entry badge — بطاقة الدخول (Page 032, `#32`)

- **Route:** `/badge` (`RouteNames.badge`, bottom-nav tab). Access: **Signed-in** (badge issued on approval).
- **Figma:** **758:1469** ("QR" — per the screen doc). **Clean-code freeze:** D-633 (2026-07-04).
- **Node inconsistency FLAGGED (not resolved):** the class doc cites `758:1469` while `badge_screen_test.dart`'s group name cites `221:769`. Same screen, two frame ids in the codebase — surfaced for the owner (as with session_moderate D-622), not guessed.

## Purpose

The visitor's entry badge: on approval the My-Area dashboard
(`GET /app/account/dashboard`, `RequireApprovedAccount`) supplies the identity and
the QR encodes the opaque `qrId`. The gold-bordered white card holds the QR + the
"امسح للدخول" hint + the gold identity strip (avatar, name, tier, the masked
`ID · ••••` reference), with role-based actions below. Not-approved / pending /
load-failure keep their respective states.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `badge_screen.dart` (158) | `BadgeScreen` + State — the approval-gated dashboard load, the loading / not-approved / error / pending / badge dispatch (`_buildBody`), and the top-level `maskedBadgeId` helper (kept here — the badge test unit-tests it). The badge case composes the card + actions. |
| `widgets/badge_qr_card.dart` (`BadgeQrCard`) | The gold-bordered white card — the **standard square** QR (`QrImageView`, D-743; the round D-423 style was undecodable by the in-app ZXing scanner), the scan hint, and the gold identity strip (`SimfAvatar` + name + tier + `ID · {maskedId}`). |
| `widgets/badge_actions.dart` (`BadgeActions`) | The role-based actions (D-426) — a visitor's gold "امسح لإضافة شخص" (→ the fullscreen `ScanContactScreen`) + outlined "share my contact"; an exhibitor's "scan visitor" — with the shared `_actionButton` filled/outlined helper. |

The off-states already used the shared `SimfEmptyState`/`SimfErrorState` (kept).
Screen was already fully tokenised (no raw `Color(0x..)`). `maskedBadgeId` stays in
the screen (the test imports it); the card receives the pre-masked string. Every
file ≤400 lines.

## L4 Figma parity

No golden — the QR + the three-provider auth/dashboard/reference setup make a
golden heavy, and this freeze is a **verbatim `_Badge` split** (no token/DRY
change). The **10 badge widget tests** are the render baseline: they drive the QR
(`find.byType(QrImageView)`), the identity strip (name/tier/`ID · •••• C123`), the
role actions, and the not-approved / pending / error states — all pass unchanged,
proving the split is behaviour-identical. (Same no-golden-verbatim call as
venue_map D-615 / session_moderate D-622.)

## Level-F

Wired: QR renders the `qrId`; visitor → scan-contact (fullscreen modal) +
share-my-contact; exhibitor → scan-visitor; retry re-fetches the dashboard;
not-approved / pending states gate the QR. Reads `GET /app/account/dashboard` +
`referenceNumberProvider`. No missing API.

## Tests

`test/features/badge/badge_screen_test.dart` (QR + identity render, `maskedBadgeId`
unit test, Arabic name, not-approved, pending, error+retry, role actions). E2E:
`docs/tests/e2e/mobile-badge.md`.

## Related decisions

- **D-633** (this clean-code freeze — `_Badge` split into `BadgeQrCard` + `BadgeActions`; node inconsistency flagged).
- **D-320** (screen built), **D-423** (circle QR — visual style superseded by D-743), **D-426** (role-based actions).
- **D-743** (badge QR → standard **square** so the in-app `flutter_zxing` scanner can decode it; the round style read on a phone camera but not in-app. Same change fixed gate / exhibitor / contact scanners via the shared `SimfScannerBody` full-frame `cropPercent`).
