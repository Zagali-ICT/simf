import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_filter_search_field.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/delegation_models.dart';
import 'delegation_card.dart';
import 'delegations_stats_strip.dart';

/// The loaded Delegations list — the stats strip, the search box, then the
/// filtered per-country cards (or the empty / no-results state).
class DelegationsBody extends StatelessWidget {
  const DelegationsBody({
    required this.data,
    required this.query,
    required this.isArabic,
    required this.l10n,
    required this.searchController,
    required this.onQueryChanged,
    super.key,
  });

  final Delegations data;
  final String query;
  final bool isArabic;
  final AppL10n l10n;
  final TextEditingController searchController;
  final ValueChanged<String> onQueryChanged;

  @override
  Widget build(BuildContext context) {
    final filtered =
        data.items.where((item) => item.matches(query)).toList(growable: false);
    final flags = data.items
        .map((item) => item.flagEmoji)
        .where((flag) => flag.isNotEmpty)
        .toList(growable: false);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        DelegationsStatsStrip(
          countryCount: data.countryCount,
          totalParticipants: data.totalParticipants,
          flags: flags,
          l10n: l10n,
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfFilterSearchField(
          controller: searchController,
          hint: l10n.delegationsSearchHint,
          onChanged: onQueryChanged,
        ),
        const SizedBox(height: SimfTokens.space4),
        if (filtered.isEmpty)
          Padding(
            padding: const EdgeInsets.only(top: SimfTokens.space8),
            child: SimfEmptyState(
              icon: Icons.flag_outlined,
              message: data.items.isEmpty
                  ? l10n.delegationsEmpty
                  : l10n.delegationsNoResults,
            ),
          )
        else
          for (final item in filtered) ...<Widget>[
            DelegationCard(item: item, isArabic: isArabic, l10n: l10n),
            const SizedBox(height: SimfTokens.space3),
          ],
      ],
    );
  }
}
