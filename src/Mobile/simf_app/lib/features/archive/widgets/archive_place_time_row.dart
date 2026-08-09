import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/archive/widgets/labelled_bullet.dart';

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
            child: LabelledBullet(
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
            child: LabelledBullet(
              label: l10n.archiveTimeLabel,
              value: dateLabel,
            ),
          ),
        ],
      ),
    );
  }
}

