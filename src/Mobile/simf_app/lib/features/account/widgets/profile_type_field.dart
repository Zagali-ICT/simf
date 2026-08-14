import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';
import 'package:simf_app/features/account/data/profile_models.dart';

/// The visitor **classification** picker on the sign-up profile step.
///
/// D-375 — the field is ALWAYS visible under "Other": loading, an inline retry
/// on failure/empty, or the loaded dropdown. It is never silently hidden, which
/// is why the three states live here rather than in a nullable field.
///
/// Presentation only: the screen owns the lookup state and the selection, and
/// is the only thing that calls setState. Extracted from
/// `sign_up_visitor_screen`.
class ProfileTypeField extends StatelessWidget {
  const ProfileTypeField({
    required this.l10n,
    required this.loading,
    required this.failed,
    required this.items,
    required this.selectedId,
    required this.showError,
    required this.onRetry,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;
  final bool loading;
  final bool failed;
  final List<ProfileTypeItem> items;
  final String? selectedId;

  /// True once a blocked Next has flagged the empty pick (the required check
  /// lives in the screen's `_next()`, not in a form validator).
  final bool showError;

  final VoidCallback onRetry;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return _labelled(
        InputDecorator(
          decoration: simfFieldDecoration(),
          child: Row(
            children: <Widget>[
              const SizedBox(
                width: SimfTokens.space4,
                height: SimfTokens.space4,
                child: CircularProgressIndicator(
                  strokeWidth: SimfTokens.profileTypeFieldStrokeWidth,
                  color: SimfTokens.accent,
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Text(l10n.loadingLabel, style: SimfTokens.bodyGreyMd),
            ],
          ),
        ),
      );
    }
    if (failed || items.isEmpty) {
      return _labelled(
        InputDecorator(
          decoration: simfFieldDecoration(),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  l10n.lookupLoadError,
                  style: SimfTokens.labelDangerSm,
                ),
              ),
              TextButton(
                key: const ValueKey<String>('profileTypeRetry'),
                onPressed: onRetry,
                style: TextButton.styleFrom(foregroundColor: SimfTokens.accent),
                child: Text(l10n.retryLabel),
              ),
            ],
          ),
        ),
      );
    }
    // D-722 — profile types are few, so this is a plain dropdown rather than
    // the shared full-screen searchable sheet (nationality / birth-region /
    // plate keep the sheet; those lists are long).
    return _labelled(
      DropdownButtonFormField<String>(
        key: const ValueKey<String>('profileTypeDropdown'),
        initialValue: selectedId,
        isExpanded: true,
        style: simfInputStyle,
        icon: const Icon(
          Icons.keyboard_arrow_down,
          color: SimfTokens.inputInk,
        ),
        decoration: simfFieldDecoration(
          errorText: showError ? l10n.profileTypeRequired : null,
        ).copyWith(
          // A DropdownButton's dense content floor is a fixed 24px (vs a text
          // field's ~21px line box), so with the shared 15px vertical inset
          // this field renders ~4px taller than its siblings. Trim the vertical
          // inset so it lands at their 50px height (24 + 2*13); 14px horizontal
          // is the shared field inset.
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 13,
          ),
        ),
        dropdownColor: SimfTokens.surface,
        hint: Text(
          l10n.profileTypeLabel,
          style: simfInputStyle.copyWith(color: SimfTokens.greyText),
        ),
        items: <DropdownMenuItem<String>>[
          for (final ProfileTypeItem t in items)
            DropdownMenuItem<String>(
              value: t.id,
              child: Text(
                l10n.isArabic ? t.nameArabic : t.name,
                overflow: TextOverflow.ellipsis,
              ),
            ),
        ],
        onChanged: onChanged,
      ),
    );
  }

  Widget _labelled(Widget field) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.profileTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        field,
      ],
    );
  }
}
