import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/account/widgets/auth_chrome.dart';

/// The sign-in card's alternative entry methods below the "or" divider:
/// Face-ID sign-in (shown only when this install has an enrolled credential and
/// the device can prompt for it), the printed-badge
/// QR sign-in (Part B, D-430 — a deliberate addition beyond Figma 168:2800),
/// and the guest-mode link (627:2390). Extracted so the screen composes this
/// block rather than defining it inline.
class SignInAltActions extends StatelessWidget {
  const SignInAltActions({
    required this.busy,
    required this.onBiometric,
    required this.onBadge,
    required this.onGuest,
    super.key,
    this.biometricLabel,
    this.biometricBlockedHint,
  });

  /// The Face-ID button's label, or null to hide the button entirely.
  ///
  /// Null covers both "this device has no usable biometric" and "no account is
  /// enrolled on this install" — in either case the button could only ever
  /// error, and the screen already documents that showing-then-erroring is the
  /// behaviour to avoid. When non-null it NAMES the account the credential
  /// opens, because the credential, not the form, decides that.
  final String? biometricLabel;

  /// Non-null when the typed address is not the one the enrolled credential
  /// opens: the button stays visible but disabled, and this reads beneath it.
  /// Visible-but-disabled rather than hidden, because a button that vanishes as
  /// you type reads as a bug and explains nothing.
  final String? biometricBlockedHint;

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
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
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
        if (biometricLabel != null) ...<Widget>[
          AuthAltButton(
            label: biometricLabel!,
            icon: const SimfSvgIcon(
              AppAssets.authFaceId,
              size: SimfTokens.signInAltActionsSize,
              color: SimfTokens.goldSoft,
            ),
            onPressed:
                (busy || biometricBlockedHint != null) ? null : onBiometric,
          ),
          if (biometricBlockedHint != null)
            Padding(
              padding: const EdgeInsets.only(top: SimfTokens.space2),
              child: Text(
                biometricBlockedHint!,
                textAlign: TextAlign.start,
                style: SimfTokens.bodyGreySm,
              ),
            ),
          const SizedBox(height: SimfTokens.space3),
        ],
        AuthAltButton(
          label: l10n.badgeSignInButton,
          icon: const Icon(
            Icons.qr_code_scanner,
            size: SimfTokens.signInAltActionsSize,
            color: SimfTokens.goldSoft,
          ),
          onPressed: busy ? null : onBadge,
        ),
        // Guest entry (Figma 627:2390, D-363) — the underlined design-native
        // link; the app's only path into guest mode (Page_012).
        SizedBox(
          height: SimfTokens.signInAltActionsHeight,
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
