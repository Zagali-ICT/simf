import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';
import '../data/profile_models.dart';

/// The organisation (جهة العمل) debounced type-ahead on the sign-up profile step.
///
/// Two shapes: once a row is picked it collapses to the chosen name plus a Clear
/// action; until then it is a search field over the live results. D-375 — fetch
/// state comes FIRST (spinner while searching, retry on failure), so "no matches"
/// only ever describes a COMPLETED empty search.
///
/// Presentation only: the screen owns the controller, the debounce and the
/// results. Extracted from `sign_up_visitor_screen`.
class OrganisationTypeaheadField extends StatelessWidget {
  const OrganisationTypeaheadField({
    required this.l10n,
    required this.controller,
    required this.selectedId,
    required this.selectedLabel,
    required this.searching,
    required this.searchFailed,
    required this.results,
    required this.showError,
    required this.onSearchChanged,
    required this.onRetry,
    required this.onSelected,
    required this.onCleared,
    super.key,
  });

  final AppL10n l10n;
  final TextEditingController controller;
  final String? selectedId;
  final String? selectedLabel;
  final bool searching;
  final bool searchFailed;
  final List<OrganisationItem> results;

  /// B3 (D-221) — organisation is required; flag an empty pick after a submit.
  final bool showError;

  final ValueChanged<String> onSearchChanged;
  final VoidCallback onRetry;
  final ValueChanged<OrganisationItem> onSelected;
  final VoidCallback onCleared;

  @override
  Widget build(BuildContext context) {
    if (selectedId != null) {
      return _labelled(<Widget>[
        InputDecorator(
          decoration: simfFieldDecoration(),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  selectedLabel ?? l10n.organisationSelected,
                  style: simfInputStyle,
                ),
              ),
              TextButton(
                onPressed: onCleared,
                style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
                child: Text(l10n.clearLabel),
              ),
            ],
          ),
        ),
      ]);
    }
    return _labelled(<Widget>[
      TextField(
        controller: controller,
        style: simfInputStyle,
        decoration: simfFieldDecoration(
          hintText: l10n.organisationSearchHint,
          prefixIcon:
              const Icon(Icons.search, color: SimfTokens.greyText, size: 18),
          errorText: showError ? l10n.organisationRequired : null,
        ),
        onChanged: onSearchChanged,
      ),
      if (controller.text.trim().isNotEmpty) ...<Widget>[
        const SizedBox(height: SimfTokens.space2),
        if (searching)
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Row(
              children: <Widget>[
                const SizedBox(
                  width: 14,
                  height: 14,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: SimfTokens.accent,
                  ),
                ),
                const SizedBox(width: 10),
                Text(l10n.loadingLabel, style: SimfTokens.bodyGrey),
              ],
            ),
          )
        else if (searchFailed)
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    l10n.lookupLoadError,
                    style: SimfTokens.labelDangerSm,
                  ),
                ),
                TextButton(
                  key: const ValueKey<String>('organisationRetry'),
                  onPressed: onRetry,
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.accent,
                  ),
                  child: Text(l10n.retryLabel),
                ),
              ],
            ),
          )
        else if (results.isEmpty)
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Text(l10n.organisationEmpty, style: SimfTokens.bodyGrey),
          )
        else
          ...results.take(8).map(
                (organisation) => ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  title: Text(
                    l10n.isArabic
                        ? organisation.nameAr
                        : (organisation.nameEn ?? organisation.nameAr),
                    style: SimfTokens.bodyHeadlineInk,
                  ),
                  subtitle: organisation.city == null
                      ? null
                      : Text(organisation.city!, style: SimfTokens.bodyGrey),
                  onTap: () => onSelected(organisation),
                ),
              ),
      ],
    ]);
  }

  Widget _labelled(List<Widget> children) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.organisationLabel),
        const SizedBox(height: SimfTokens.space2),
        ...children,
      ],
    );
  }
}
