import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import 'auth_chrome.dart';

// The Face-ID glyph from frame 168:2845 (mingcute:faceid-line) — no 1:1
// Material match, so it ships as an iconify SVG asset.
const String _icFaceId = 'assets/icons/auth_faceid.svg';

/// The sign-in card's alternative entry methods below the "or" divider:
/// Face-ID sign-in (shown only when a biometric is usable), the printed-badge
/// QR sign-in (Part B, D-430 — a deliberate addition beyond Figma 168:2800),
/// and the guest-mode link (627:2390). Extracted so the screen composes this
/// block rather than defining it inline.
class SignInAltActions extends StatelessWidget {
  const SignInAltActions({
    required this.biometricAvailable,
    required this.busy,
    required this.onBiometric,
    required this.onBadge,
    required this.onGuest,
    super.key,
  });

  /// Whether the device has a usable biometric — the Face-ID button is hidden
  /// entirely on sensorless devices rather than shown then erroring.
  final bool biometricAvailable;

  /// While true every action is disabled (a sign-in is in flight).
  final bool busy;

  final VoidCallback onBiometric;
  final VoidCallback onBadge;
  final VoidCallback onGuest;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Row(
          children: <Widget>[
            const Expanded(
              child: Divider(color: SimfTokens.beigeBorder, height: 1),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                l10n.orDividerLabel,
                style: const TextStyle(
                  fontSize: 12,
                  color: SimfTokens.greyText,
                ),
              ),
            ),
            const Expanded(
              child: Divider(color: SimfTokens.beigeBorder, height: 1),
            ),
          ],
        ),
        const SizedBox(height: 24),
        if (biometricAvailable) ...<Widget>[
          AuthAltButton(
            label: l10n.faceIdSignInButton,
            icon: const SimfSvgIcon(
              _icFaceId,
              size: 20,
              color: SimfTokens.goldSoft,
            ),
            onPressed: busy ? null : onBiometric,
          ),
          const SizedBox(height: 12),
        ],
        AuthAltButton(
          label: l10n.badgeSignInButton,
          icon: const Icon(
            Icons.qr_code_scanner,
            size: 20,
            color: SimfTokens.goldSoft,
          ),
          onPressed: busy ? null : onBadge,
        ),
        // Guest entry (Figma 627:2390, D-363) — the underlined design-native
        // link; the app's only path into guest mode (Page_012).
        SizedBox(
          height: 48,
          child: Center(
            child: TextButton(
              onPressed: busy ? null : onGuest,
              style: authLinkButtonStyle(SimfTokens.greyText),
              child: Text(
                l10n.guestSignInLink,
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  decoration: TextDecoration.underline,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
