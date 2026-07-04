import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// A titled group of nav rows (frames 1129:17… "معلومات الملتقى" etc.).
class MoreSection extends StatelessWidget {
  const MoreSection({required this.title, required this.rows, super.key});

  final String title;
  final List<Widget> rows;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.only(bottom: SimfTokens.space3),
          child: Text(
            title,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        for (final (index, row) in rows.indexed) ...<Widget>[
          if (index > 0) const SizedBox(height: SimfTokens.space2),
          row,
        ],
      ],
    );
  }
}

/// One nav row (frame 1129:17225…): the navy-deep box with the title at the
/// inline start, an optional value, and the gold caret at the inline end.
class MoreRow extends StatelessWidget {
  const MoreRow({
    required this.title,
    required this.onTap,
    this.trailingValue,
    super.key,
  });

  final String title;
  final String? trailingValue;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: 48,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    title,
                    textAlign: TextAlign.start,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
                if (trailingValue != null) ...<Widget>[
                  const SizedBox(width: SimfTokens.space2),
                  Text(
                    trailingValue!,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
                const SizedBox(width: SimfTokens.space2),
                const SimfSvgIcon(
                  'assets/icons/ic_caret_left.svg',
                  color: SimfTokens.accent,
                  size: 22,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
