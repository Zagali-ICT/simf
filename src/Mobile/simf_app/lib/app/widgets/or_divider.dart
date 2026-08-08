import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold "or" divider between the camera and manual paths.
class OrDivider extends StatelessWidget {
  const OrDivider({required this.label});

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
