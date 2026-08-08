import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// A small icon + text meta row (date / location) under the hero title.
class MetaLine extends StatelessWidget {
  const MetaLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.surface),
        const SizedBox(width: SimfTokens.space1),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.bodyWhiteSm,
          ),
        ),
      ],
    );
  }
}
