import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';
import 'package:simf_app/features/archive/widgets/edition_pill.dart';

/// The edition-selector pills (frame node 925:3248): one pill per edition,
/// **equal-width** and filling the row (frame `flex-1`, 16px gap). The selected
/// pill is solid gold (white text); the rest are bordered navy cards with beige
/// text. Equal-flex means N pills always fit (they share the width); the frame
/// shows three.
class ArchiveEditionPills extends StatelessWidget {
  const ArchiveEditionPills({
    required this.editions,
    required this.selectedId,
    required this.onSelect,
    super.key,
  });

  final List<ArchiveEdition> editions;
  final String selectedId;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Row(
      children: <Widget>[
        for (var i = 0; i < editions.length; i++) ...<Widget>[
          if (i > 0) const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: EditionPill(
              label: l10n.archiveEditionPill(editions[i].year),
              active: editions[i].id == selectedId,
              onTap: () => onSelect(editions[i].id),
            ),
          ),
        ],
      ],
    );
  }
}

