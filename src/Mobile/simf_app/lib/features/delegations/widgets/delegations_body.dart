import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_filter_search_field.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/widgets/active_filter_chip.dart';
import 'package:simf_app/features/delegations/widgets/delegation_card.dart';
import 'package:simf_app/features/delegations/widgets/delegations_stats_strip.dart';

class DelegationsBody extends StatelessWidget {
  const DelegationsBody({
    required this.data,
    required this.query,
    required this.isArabic,
    required this.l10n,
    required this.searchController,
    required this.onQueryChanged,
    required this.selectedCountryCode,
    required this.onFlagTap,
    required this.onClearFilter,
    this.onRequestMeeting,
    super.key,
  });

  final Delegations data;
  final String query;
  final bool isArabic;
  final AppL10n l10n;
  final TextEditingController searchController;
  final ValueChanged<String> onQueryChanged;

  final String? selectedCountryCode;

  final ValueChanged<String> onFlagTap;

  final VoidCallback onClearFilter;

  /// Bi-Meeting rework — when set (the user holds AllowsDelegationMeeting),
  /// fired with a delegation when its card is tapped to request a meeting with
  /// it.
  final void Function(DelegationItem delegation)? onRequestMeeting;

  @override
  Widget build(BuildContext context) {
    final flagItems = data.flagItems;
    final filtered =
        data.visible(query: query, countryCode: selectedCountryCode);
    final selectedName =
        data.selectedCountryName(selectedCountryCode, isArabic: isArabic);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        DelegationsStatsStrip(
          countryCount: data.countryCount,
          flagItems: flagItems,
          selectedCountryCode: selectedCountryCode,
          onFlagTap: onFlagTap,
          l10n: l10n,
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfFilterSearchField(
          controller: searchController,
          hint: l10n.delegationsSearchHint,
          onChanged: onQueryChanged,
        ),
        // Show the chip whenever a flag filter is active — falling back to the
        // clear label if the selected country vanished from the data (e.g. a
        // backend removal + refresh) so the filter can never get stuck with no
        // way out.
        if (selectedCountryCode != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          ActiveFilterChip(
            country: selectedName ?? l10n.delegationsClearFilter,
            clearLabel: l10n.delegationsClearFilter,
            onClear: onClearFilter,
          ),
        ],
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
            DelegationCard(
              item: item,
              isArabic: isArabic,
              onTap: onRequestMeeting == null
                  ? null
                  : () => onRequestMeeting!(item),
            ),
            const SizedBox(height: SimfTokens.space3),
          ],
      ],
    );
  }

}
