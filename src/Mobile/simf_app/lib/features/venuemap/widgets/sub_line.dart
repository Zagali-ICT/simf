import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The "Exhibitor · Sector" sub-line; renders only the parts that are present.
class SubLine extends StatelessWidget {
  const SubLine(this.exhibitor, this.sector);

  final String? exhibitor;
  final String? sector;

  @override
  Widget build(BuildContext context) {
    final parts = <String>[
      if (exhibitor != null) exhibitor!,
      if (sector != null) sector!,
    ];
    if (parts.isEmpty) {
      return const SizedBox.shrink();
    }
    return Text(
      parts.join(' · '),
      style: SimfTokens.bodyInkMuted,
    );
  }
}
