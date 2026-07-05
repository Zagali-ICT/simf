import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import 'archive_bullet.dart';

/// The المكان / الزمن two-column row (node 926:3284): a gold inline-start
/// divider between the place column and the time column.
class ArchivePlaceTimeRow extends StatelessWidget {
  const ArchivePlaceTimeRow({
    required this.l10n,
    required this.location,
    required this.dateLabel,
    super.key,
  });

  final AppL10n l10n;
  final String? location;
  final String? dateLabel;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // Frame 926:3284 — RTL, verified against the rendered frame: المكان
          // (place) at the inline start (right), الزمن (time) at the inline end
          // (left).
          Expanded(
            child: _LabelledBullet(
              label: l10n.archivePlaceLabel,
              value: location,
            ),
          ),
          Container(
            width: SimfTokens.hairlineBold,
            color: SimfTokens.accent,
            margin: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          ),
          Expanded(
            child: _LabelledBullet(
              label: l10n.archiveTimeLabel,
              value: dateLabel,
            ),
          ),
        ],
      ),
    );
  }
}

/// A white label over an optional beige bulleted value (one column of the
/// المكان / الزمن row).
class _LabelledBullet extends StatelessWidget {
  const _LabelledBullet({required this.label, required this.value});

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
