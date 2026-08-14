import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/questions/widgets/numbered_line.dart';

/// The frame 1049:12590 "بيانات الجلسة" block: the white Medium section header
/// over the session-data lines rendered as a right-aligned numbered list
/// (frame 1049:12591-12594), each line `#C2B8A2` 14px Medium.
class SessionDataBlock extends StatelessWidget {
  const SessionDataBlock({required this.label, required this.lines, super.key});

  final String label;
  final List<String> lines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Text(
          label,
          // TextAlign.start = right under RTL (TextAlign.end would be left).
          textAlign: TextAlign.start,
          style: SimfTokens.labelWhiteMediumLg,
        ),
        // Frame 1049:12590 — 8px under the label, 16px between data lines.
        const SizedBox(height: SimfTokens.space2),
        for (var i = 0; i < lines.length; i++) ...<Widget>[
          if (i != 0) const SizedBox(height: SimfTokens.space4),
          NumberedLine(index: i + 1, text: lines[i]),
        ],
      ],
    );
  }
}
