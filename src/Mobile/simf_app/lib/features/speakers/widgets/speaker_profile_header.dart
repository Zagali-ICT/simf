import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/speakers/widgets/name_line.dart';

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
            width: SimfTokens.speakerProfileHeaderWidth,
            height: SimfTokens.speakerProfileHeaderHeight,
            child: SimfCircledBackButton(onBack: onBack),
          ),
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                NameLine(title: title, flag: flag),
                if (rank != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    rank!,
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: SimfTokens.labelBeigeSemibold,
                  ),
                ],
              ],
            ),
          ),
          // Balances the leading back button so the two-line title stays
          // centred.
          const SizedBox(
              width: SimfTokens.speakerProfileHeaderWidth,
              height: SimfTokens.speakerProfileHeaderHeight,),
        ],
      ),
    );
  }
}
