import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// A speaker's name with the country flag as an inline glyph at its trailing
/// (left, in RTL) edge — Figma node **1318:3391**: flag left of the name, an
/// 8px gap, the name ellipsising when it is too long. NOT a badge on the
/// avatar.
///
/// Written out twice, in the speakers-list card and in the bilateral picker's
/// option tile, which differ only in text style and alignment. Distinct from
/// `NameLine`, which is the profile header's centred, LTR-forced title.
class SpeakerNameWithFlag extends StatelessWidget {
  const SpeakerNameWithFlag({
    required this.name,
    required this.flag,
    required this.style,
    this.textAlign,
    super.key,
  });

  final String name;

  /// The flag emoji, or null when the speaker has no nationality.
  final String? flag;
  final TextStyle style;
  final TextAlign? textAlign;

  @override
  Widget build(BuildContext context) {
    final glyph = flag;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Flexible(
          child: Text(
            name,
            textAlign: textAlign,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: style,
          ),
        ),
        if (glyph != null) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          Text(
            glyph,
            textDirection: TextDirection.ltr,
            // Frame 1318:3392 — the flag glyph is 12px.
            style: const TextStyle(
              fontSize: SimfTokens.textSm,
              height: 1,
            ),
          ),
        ],
      ],
    );
  }
}
