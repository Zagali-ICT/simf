import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/archive_models.dart';

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
            child: _EditionPill(
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

class _EditionPill extends StatelessWidget {
  const _EditionPill({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: active
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        onTap: active ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: 48,
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: active ? Colors.white : SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}
