import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The 32px navy file-icon box (Figma 1388:7643 — no border, a 20px beige glyph).
class FileIcon extends StatelessWidget {
  const FileIcon();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.requestIconBox,
      height: SimfTokens.requestIconBox,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: const Icon(
        Icons.description_outlined,
        size: SimfTokens.fileIconSize,
        color: SimfTokens.beigeBorder,
      ),
    );
  }
}
