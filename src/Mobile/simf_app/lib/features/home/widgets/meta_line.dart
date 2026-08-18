import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class MetaLine extends StatelessWidget {
  const MetaLine({required this.icon, required this.text, super.key});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: SimfTokens.metaLineSize, color: SimfTokens.surface),
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
