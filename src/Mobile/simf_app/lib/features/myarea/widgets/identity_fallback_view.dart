import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// Shown when no live camera is available (web / test / no camera / permission
/// denied / a device without Google Play Services): a "camera required" message
/// with a retry. There is NO gallery option — the identity photo must be a live
/// camera image (owner 2026-07-06, D-662).
class IdentityFallbackView extends StatelessWidget {
  const IdentityFallbackView({
    required this.l10n,
    required this.onRetry,
    super.key,
  });

  final AppL10n l10n;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.photo_camera_front_outlined,
              size: 56,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space4),
            Text(
              l10n.identityCameraUnavailable,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: SimfTokens.space6),
            FilledButton.icon(
              onPressed: onRetry,
              style: FilledButton.styleFrom(
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.navy,
                minimumSize: const Size.fromHeight(48),
              ),
              icon: const Icon(Icons.refresh),
              label: Text(l10n.identityRetry),
            ),
          ],
        ),
      ),
    );
  }
}
