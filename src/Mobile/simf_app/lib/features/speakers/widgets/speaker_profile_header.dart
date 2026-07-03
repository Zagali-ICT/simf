import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';

/// The speaker-profile header (frame 908:2110): the two-line title (white name
/// over the beige rank, with the nationality flag leading the name) flanked by
/// the circled back chevron — replaces the shell's default single-line title.
class SpeakerProfileHeader extends StatelessWidget {
  const SpeakerProfileHeader({
    required this.title,
    required this.rank,
    required this.flag,
    required this.onBack,
    super.key,
  });

  final String title;
  final String? rank;
  final String flag;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: 42,
            height: 42,
            child: SimfCircledBackButton(onBack: onBack),
          ),
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                _NameLine(title: title, flag: flag),
                if (rank != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    rank!,
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w600,
                      color: SimfTokens.beigeBorder,
                    ),
                  ),
                ],
              ],
            ),
          ),
          // Balances the leading back button so the two-line title stays centred.
          const SizedBox(width: 42, height: 42),
        ],
      ),
    );
  }
}

/// The header name line. Figma 1327:3461 — when the speaker has a nationality
/// the flag sits at the inline-start of the name (physical left in this
/// LTR-forced header), 8px before it; the name ellipsises if it is too long.
class _NameLine extends StatelessWidget {
  const _NameLine({required this.title, required this.flag});

  final String title;
  final String flag;

  @override
  Widget build(BuildContext context) {
    final nameText = Text(
      title,
      textAlign: TextAlign.center,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: const TextStyle(
        fontSize: SimfTokens.textTitle,
        fontWeight: FontWeight.w600,
        color: Colors.white,
      ),
    );
    if (flag.isEmpty) {
      return nameText;
    }
    return Row(
      textDirection: TextDirection.ltr,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(flag, style: const TextStyle(fontSize: SimfTokens.textTitle)),
        const SizedBox(width: SimfTokens.space2),
        Flexible(child: nameText),
      ],
    );
  }
}
