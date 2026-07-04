import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/contact_models.dart';

/// One saved-contact row — name, an org/title subtitle, chevron. Tapping opens
/// the detail sheet. (Named `Tile` to avoid clashing with the `SavedContactRow`
/// data model it renders.)
class SavedContactTile extends StatelessWidget {
  const SavedContactTile({
    required this.row,
    required this.isArabic,
    required this.onTap,
    super.key,
  });

  final SavedContactRow row;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final name = row.subjectAvailable
        ? row.localizedName(isArabic)
        : l10n.contactUnavailable;
    final subtitleParts = <String>[
      if (row.jobTitle != null && row.jobTitle!.trim().isNotEmpty) row.jobTitle!,
      if (row.organisation != null && row.organisation!.trim().isNotEmpty)
        row.organisation!,
    ];
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        leading: const Icon(
          Icons.account_circle_outlined,
          color: SimfTokens.accent,
        ),
        title: Text(name),
        subtitle: subtitleParts.isEmpty ? null : Text(subtitleParts.join(' · ')),
        trailing: const Icon(Icons.chevron_right, color: SimfTokens.inkMuted),
        onTap: onTap,
      ),
    );
  }
}
