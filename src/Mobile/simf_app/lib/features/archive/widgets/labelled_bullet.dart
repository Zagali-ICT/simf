import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import 'archive_bullet.dart';

/// A white label over an optional beige bulleted value (one column of the
/// المكان / الزمن row).
class LabelledBullet extends StatelessWidget {
  const LabelledBullet({required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SimfSectionHeader(title: label),
        if (value != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          ArchiveBullet(text: value!, color: SimfTokens.beigeBorder),
        ],
      ],
    );
  }
}
