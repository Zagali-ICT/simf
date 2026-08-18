import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_picker_field.dart';

/// 19j — a captioned lookup that opens the shared searchable sheet, exactly
/// like Create-profile, instead of a raw Material dropdown. The walk-in form
/// uses it for the visitor classification, the nationality and the
/// organisation; a null [onTap] renders it un-openable (DEF-STF-007, an empty
/// lookup) while [errorText] says why.
class StaffLookupField extends StatelessWidget {
  const StaffLookupField({
    required this.label,
    required this.fieldKey,
    required this.displayText,
    required this.isPlaceholder,
    required this.onTap,
    this.errorText,
    super.key,
  });

  final String label;
  final String fieldKey;
  final String displayText;
  final bool isPlaceholder;
  final VoidCallback? onTap;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: label,
          child: SimfPickerField(
            fieldKey: fieldKey,
            displayText: displayText,
            isPlaceholder: isPlaceholder,
            onTap: onTap,
            errorText: errorText,
          ),
        ),
      ],
    );
  }
}
