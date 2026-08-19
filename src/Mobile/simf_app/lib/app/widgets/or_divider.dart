import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class OrDivider extends StatelessWidget {
  const OrDivider({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Expanded(child: Divider(color: SimfTokens.accent)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
          child: Text(
            label,
            style: SimfTokens.labelBeigeSm,
          ),
        ),
        const Expanded(child: Divider(color: SimfTokens.accent)),
      ],
    );
  }
}
