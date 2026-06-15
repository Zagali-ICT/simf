import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The camera-off prompt shared by the QR scanners. The live camera starts only
/// when the user taps this, so the on-screen back / cancel / manual-entry stay
/// tappable beforehand — the fix for devices (Huawei/EMUI) where the live
/// `mobile_scanner` camera grabs every on-screen tap window-wide (D-426).
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
