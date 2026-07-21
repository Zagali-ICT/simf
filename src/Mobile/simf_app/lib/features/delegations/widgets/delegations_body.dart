import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_filter_search_field.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/delegation_models.dart';
import 'delegation_card.dart';
import 'delegations_stats_strip.dart';

/// The loaded Delegations list — the stats strip, the search box, then the
/// filtered per-country cards (or the empty / no-results state). Tapping a flag
/// in the stats strip narrows the list to one country ([selectedCountryCode]);
/// the active-filter chip below the search box clears it.
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
    super.key,
  });

  final Delegations data;
  final String query;
  final bool isArabic;
  final AppL10n l10n;
  final TextEditingController searchController;
  final ValueChanged<String> onQueryChanged;

  /// The flag-filtered country's code (from a stats-strip tap), or null.
  final String? selectedCountryCode;

  /// Fired with a country code when its stats-strip flag is tapped.
  final ValueChanged<String> onFlagTap;

  /// Clears the flag filter (from the active-filter chip).
  final VoidCallback onClearFilter;

  @override
  Widget build(BuildContext context) {
    final flagItems = data.items
        .where((item) => item.flagEmoji.isNotEmpty)
        .toList(growable: false);
    final filtered = data.items
        .where(
          (item) =>
              (selectedCountryCode == null ||
                  item.countryCode == selectedCountryCode) &&
              item.matches(query),
        )
        .toList(growable: false);
    final selectedName = _selectedCountryName();

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        DelegationsStatsStrip(
          countryCount: data.countryCount,
          totalParticipants: data.totalParticipants,
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
          _ActiveFilterChip(
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
            DelegationCard(item: item, isArabic: isArabic, l10n: l10n),
            const SizedBox(height: SimfTokens.space3),
          ],
      ],
    );
  }

  /// The localized name of the flag-filtered country (for the active-filter
  /// chip), or null when no flag filter is active.
  String? _selectedCountryName() {
    if (selectedCountryCode == null) {
      return null;
    }
    for (final item in data.items) {
      if (item.countryCode == selectedCountryCode) {
        return item.localizedCountry(isArabic);
      }
    }
    return null;
  }
}

/// The removable filter pill shown when a stats-strip flag is selected: the
/// country name with a close glyph; the whole pill clears the filter.
class _ActiveFilterChip extends StatelessWidget {
  const _ActiveFilterChip({
    required this.country,
    required this.clearLabel,
    required this.onClear,
  });

  final String country;
  final String clearLabel;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: onClear,
        child: Container(
          decoration: BoxDecoration(
            color: SimfTokens.goldFill6,
            border: Border.all(color: SimfTokens.goldBorder15),
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          padding: const EdgeInsetsDirectional.only(
            start: SimfTokens.space3,
            end: SimfTokens.space2,
            top: SimfTokens.space2,
            bottom: SimfTokens.space2,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Flexible(
                child: Text(
                  country,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelGoldSemiboldSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space1),
              // Semantics label so the tap target reads as "clear filter" to
              // assistive tech, not just a bare glyph.
              Semantics(
                button: true,
                label: clearLabel,
                child: const Icon(
                  Icons.close,
                  size: 14,
                  color: SimfTokens.accent,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
