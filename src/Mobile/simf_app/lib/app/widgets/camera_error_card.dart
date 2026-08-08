import 'package:flutter/material.dart';
import '../theme/tokens.dart';

/// Shown when the camera cannot start (permission denied / no camera / init
/// failure). Points at system settings and keeps the manual path below usable,
/// so the scanner is never a silent black dead-end.
class CameraErrorCard extends StatelessWidget {
  const CameraErrorCard({
    required this.message,
    required this.retryLabel,
    required this.onRetry,
  });

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: SimfTokens.scannerCard,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space5),
        child: Column(
          children: <Widget>[
            const Icon(
              Icons.no_photography_outlined,
              color: SimfTokens.accent,
              size: SimfTokens.cameraErrorCardSize,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              message,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
            const SizedBox(height: SimfTokens.space3),
            TextButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(retryLabel),
              style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
            ),
          ],
        ),
      ),
    );
  }
}
