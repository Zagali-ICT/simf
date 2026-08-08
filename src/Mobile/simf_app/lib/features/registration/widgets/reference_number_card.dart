import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The reference-number card (Figma 505:1525, D-366/D-373): the beige label over
/// the gold `SIMF-YYYY-NNNNNNNN` reference (forced LTR), on an 80%-navy fill.
class ReferenceNumberCard extends StatelessWidget {
  const ReferenceNumberCard({
    required this.label,
    required this.reference,
    super.key,
  });

  final String label;
  final String reference;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space6),
      decoration: BoxDecoration(
        color: SimfTokens.navyFill80,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        children: <Widget>[
          Text(
            label,
            style: SimfTokens.bodyBeigeMd,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            reference,
            textDirection: TextDirection.ltr,
            style: SimfTokens.labelGoldBoldLg,
          ),
        ],
      ),
    );
  }
}
