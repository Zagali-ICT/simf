import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The gold border weight of the viewfinder's corner brackets (Figma 758:4579).
const BorderSide _bracketSide =
    BorderSide(color: SimfTokens.accent, width: 2.36);

/// One corner bracket of the QR viewfinder: an L drawn as two borders of an
/// otherwise empty box. [top] and [left] pick which corner, so the same widget
/// renders all four.
class ScannerBracket extends StatelessWidget {
  const ScannerBracket({required this.top, required this.left, super.key});

  final bool top;
  final bool left;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.simfScannerFrameWidth,
      height: SimfTokens.simfScannerFrameHeightLg,
      decoration: BoxDecoration(
        border: Border(
          top: top ? _bracketSide : BorderSide.none,
          bottom: top ? BorderSide.none : _bracketSide,
          left: left ? _bracketSide : BorderSide.none,
          right: left ? BorderSide.none : _bracketSide,
        ),
      ),
    );
  }
}
