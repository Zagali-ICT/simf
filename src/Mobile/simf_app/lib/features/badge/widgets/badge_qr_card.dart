import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../core/utils/hex_color.dart';
import '../../myarea/data/myarea_models.dart';

/// The issued badge card (frame node tree under 758:1469): the gold-bordered
/// white card holding the QR, the "امسح للدخول" hint and the identity strip
/// (avatar, name, tier line, the masked `ID · …` reference). The strip is
/// tinted by the profile type's colour (`identity.pageColor`), falling back to
/// the token gold when the account has no type.
class BadgeQrCard extends StatelessWidget {
  const BadgeQrCard({
    required this.l10n,
    required this.identity,
    required this.qrId,
    required this.maskedId,
    super.key,
  });

  final AppL10n l10n;
  final MyAreaIdentity identity;

  /// The opaque value the QR encodes.
  final String qrId;

  /// The pre-masked human reference shown on the ID line (`•••• 1234`).
  final String maskedId;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final name = identity.localizedName(isArabic);
    final tier = identity.localizedTier(isArabic);
    // The identity strip is tinted by the profile type's colour (server value
    // `ProfileType.PageColor`, surfaced as `identity.pageColor`) so each tier's
    // badge is visually distinct; it falls back to the token gold when the
    // account has no type or the value is unset/invalid.
    final stripColor = parseHexColor(identity.pageColor) ?? SimfTokens.accent;
    // Keep the white ink used on gold and on the whole seeded palette (all
    // luminance ≤ ~0.45); only a genuinely pale admin-picked colour flips to a
    // dark ink so the name never washes out. `copyWith(color: null)` keeps each
    // style's own colour. (Promote to a shared inkOn helper at the 2nd
    // pageColor-tinted surface — §7, extract on second use.)
    final darkInk =
        stripColor.computeLuminance() > 0.6 ? SimfTokens.navyDeep : null;
    final nameStyle =
        SimfTokens.labelWhiteSemiboldTitle.copyWith(color: darkInk);
    final mutedStyle = SimfTokens.labelOnGoldMutedSm.copyWith(color: darkInk);
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.surface,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
        // Frame 758:1469 — the gold card edge is a thin 1-px stroke.
        border: Border.all(color: SimfTokens.accent, width: 1),
      ),
      child: Column(
        children: <Widget>[
          Padding(
            // Frame 758:1477 — the QR (≈265 on the 343 card) sits ~24 inset
            // inside the card padding; sized to the available width so it
            // stays proportional on any device.
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: LayoutBuilder(
              builder: (context, constraints) {
                // Keep the QR square AND fit the page: cap to ~half the
                // screen height so a rotated (landscape) layout doesn't blow
                // it up past the viewport. Portrait is unchanged — width is
                // the smaller dimension there (≈265 on the 343 card).
                final maxByHeight = MediaQuery.of(context).size.height * 0.5;
                final qrSize = constraints.maxWidth < maxByHeight
                    ? constraints.maxWidth
                    : maxByHeight;
                return QrImageView(
                  data: qrId,
                  version: QrVersions.auto,
                  size: qrSize,
                  gapless: true,
                  // Standard square QR. Do NOT re-add the round "circle" style
                  // (old D-423): round eyes + dot modules read on a phone's ML
                  // camera but the in-app ZXing reader (flutter_zxing) can NOT
                  // decode them, so every in-app badge scan (sign-in / gate /
                  // exhibitor) failed. Owner chose plain square (2026-07-11);
                  // content/data is unchanged.
                  eyeStyle: const QrEyeStyle(
                    eyeShape: QrEyeShape.square,
                    color: SimfTokens.black,
                  ),
                  dataModuleStyle: const QrDataModuleStyle(
                    dataModuleShape: QrDataModuleShape.square,
                    color: SimfTokens.black,
                  ),
                );
              },
            ),
          ),
          Text(
            l10n.badgeScanToEnter,
            // Frame 758:1469 — black, 16px, slight negative tracking.
            style: SimfTokens.bodyBlackLgTracked,
          ),
          const SizedBox(height: SimfTokens.space4),
          Container(
            padding: const EdgeInsets.all(SimfTokens.space2),
            decoration: BoxDecoration(
              color: stripColor,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
            ),
            child: Row(
              children: <Widget>[
                // Frame 758:1469 — a 64-px rounded box; the SIMF brand-mark
                // fallback on its navy box stays visible on the gold strip,
                // replaced by the photo when present. Owner 2026-07-26 — the
                // badge photo is display-only, so tapping it opens it full size.
                SimfAvatar(
                  name: name,
                  currentUser: true,
                  size: 64,
                  enableFullScreen: true,
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        // Frame 758:1469 — 18px SemiBold; ink adapts.
                        style: nameStyle,
                      ),
                      if (tier != null) ...<Widget>[
                        const SizedBox(height: SimfTokens.space2),
                        Text(
                          tier,
                          // Frame 758:1469 — 12px muted; ink adapts.
                          style: mutedStyle,
                        ),
                      ],
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        'ID · $maskedId',
                        textDirection: TextDirection.ltr,
                        // Frame 758:1469 — 12px muted; ink adapts to the strip.
                        style: mutedStyle,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
