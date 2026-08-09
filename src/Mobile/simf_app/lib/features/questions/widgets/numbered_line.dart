import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One numbered session-data line on the send-question sheet.
/// One numbered session-data line — the index sits at the inline start (right
/// in RTL) before the right-aligned beige body, matching the frame's
/// `list-decimal` marker.
class NumberedLine extends StatelessWidget {
  const NumberedLine({required this.index, required this.text, super.key});

  final int index;
  final String text;

  // Frame 1049:12591 — leading 1.3, tighter than the 1.5 body.
  static const TextStyle _style = SimfTokens.bodyBeigeMedium13;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text('$index.', textDirection: TextDirection.ltr, style: _style),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(text, textAlign: TextAlign.start, style: _style),
        ),
      ],
    );
  }
}
