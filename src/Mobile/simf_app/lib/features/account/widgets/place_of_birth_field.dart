import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/validation/field_limits.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';
import '../../../core/widgets/simf_picker_field.dart';

/// مكان الميلاد on the sign-up profile step — required for everyone (D-723).
///
/// Saudi registrants pick from the region lookup (a searchable sheet, D-547);
/// everyone else types the passport's free-text place. Which of the two renders
/// is driven by the nationality field, not by this widget.
///
/// Presentation only: the screen owns the controller and keeps the stored name
/// in sync with the active locale. Extracted from `sign_up_visitor_screen`.
class PlaceOfBirthField extends StatelessWidget {
  const PlaceOfBirthField({
    required this.l10n,
    required this.isSaudi,
    required this.controller,
    required this.regionDisplayName,
    required this.hasRegion,
    required this.showRegionError,
    required this.onPickRegion,
    super.key,
  });

  final AppL10n l10n;
  final bool isSaudi;
  final TextEditingController controller;

  /// The picked region's name in the active locale, or null when none is set.
  final String? regionDisplayName;

  final bool hasRegion;

  /// True once a blocked Next has flagged an unpicked region.
  final bool showRegionError;

  final VoidCallback onPickRegion;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.placeOfBirthLabel),
        const SizedBox(height: SimfTokens.space2),
        if (isSaudi)
          SimfPickerField(
            fieldKey: 'birthRegionPicker',
            displayText: regionDisplayName ?? l10n.placeOfBirthRegionHint,
            isPlaceholder: !hasRegion,
            onTap: onPickRegion,
            errorText: showRegionError ? l10n.placeOfBirthRequired : null,
          )
        else
          TextFormField(
            controller: controller,
            maxLength: FieldLimits.placeOfBirth,
            style: simfInputStyle,
            autovalidateMode: AutovalidateMode.onUserInteraction,
            validator: (String? v) => (v == null || v.trim().isEmpty)
                ? l10n.placeOfBirthRequired
                : null,
            decoration: simfFieldDecoration(
              counterText: '',
              hintText: l10n.placeOfBirthPassportHint,
            ),
          ),
      ],
    );
  }
}
