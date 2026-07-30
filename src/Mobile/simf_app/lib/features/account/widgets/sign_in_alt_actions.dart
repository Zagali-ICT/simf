import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import 'auth_chrome.dart';

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
              padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
              child: Text(
                l10n.orDividerLabel,
                style: SimfTokens.bodyGreySm,
              ),
            ),
            const Expanded(
              child: Divider(color: SimfTokens.beigeBorder, height: 1),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        if (biometricAvailable) ...<Widget>[
          AuthAltButton(
            label: l10n.faceIdSignInButton,
            icon: const SimfSvgIcon(
              AppAssets.authFaceId,
              size: 20,
              color: SimfTokens.goldSoft,
            ),
            onPressed: busy ? null : onBiometric,
          ),
          const SizedBox(height: SimfTokens.space3),
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
                // Explicit colour + decorationColor: the underline is dropped
                // when both are left to resolve from the button's foreground
                // (the label's DefaultTextStyle doesn't carry them to the
                // decoration painter), so the guest link rendered plain despite
                // the underline being set. Grey #6C7278 matches frame 627:2390.
                style: const TextStyle(
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w700,
                  color: SimfTokens.greyText,
                  decoration: TextDecoration.underline,
                  decorationColor: SimfTokens.greyText,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
