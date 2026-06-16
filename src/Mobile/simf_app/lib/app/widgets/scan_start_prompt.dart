import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The camera-off prompt shared by the QR scanners. The live camera starts only
/// when the user taps this, so the on-screen back / cancel / manual-entry stay
/// tappable beforehand — a guard for devices where a live camera preview can
/// grab on-screen taps until it is mounted (D-426). The scanners use the native
/// ZXing engine (flutter_zxing) so the camera also works without Google Play
/// Services (Huawei/HMS).
class ScanStartPrompt extends StatelessWidget {
  const ScanStartPrompt({super.key, required this.label, required this.onStart});

  final String label;
  final VoidCallback onStart;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.field,
      child: InkWell(
        onTap: onStart,
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const Icon(
                Icons.qr_code_scanner,
                size: 64,
                color: SimfTokens.accent,
              ),
              const SizedBox(height: SimfTokens.space3),
              FilledButton.icon(
                onPressed: onStart,
                icon: const Icon(Icons.photo_camera_outlined),
                label: Text(label),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
