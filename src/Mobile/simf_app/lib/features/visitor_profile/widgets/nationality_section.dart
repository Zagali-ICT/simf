import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_picker_field.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';

/// The nationality picker.
///
/// It is the most consequential field on the form: the pick drives whether the
/// visitor supplies a national ID or an Iqama/passport, and which mobile shape
/// applies (D-373). It shows the country's name in the reading language, and
/// falls back to the field label as a placeholder when nothing is chosen yet.
///
/// The error appears only once submit has been attempted, so an untouched form
/// is not covered in red.
class NationalitySection extends StatelessWidget {
  const NationalitySection({
    required this.l10n,
    required this.countries,
    required this.selectedCode,
    required this.showError,
    required this.onTap,
    super.key,
  });

  final AppL10n l10n;
  final List<CountryItem> countries;
  final String? selectedCode;

  final bool showError;

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final selected = countries.where((c) => c.code == selectedCode).toList();
    final hasValue = selected.isNotEmpty;
    final label = hasValue
        ? (l10n.isArabic ? selected.first.nameArabic : selected.first.name)
        : l10n.nationalityLabel;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.nationalityLabel),
        const SizedBox(height: SimfTokens.space2),
        SimfPickerField(
          fieldKey: 'nationalityPicker',
          displayText: label,
          isPlaceholder: !hasValue,
          onTap: onTap,
          errorText: showError ? l10n.nationalityRequired : null,
        ),
      ],
    );
  }
}
