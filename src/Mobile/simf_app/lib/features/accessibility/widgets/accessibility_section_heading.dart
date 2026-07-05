import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// A section heading on the accessibility screen (العرض · الصوت والقراءة). Kept
/// screen-local, not `SimfSectionHeader` — this heading is `textLg`/`w600`, a
/// heavier weight than the shared header's `w500`.
class AccessibilitySectionHeading extends StatelessWidget {
  const AccessibilitySectionHeading(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w600,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}
