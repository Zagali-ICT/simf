import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import 'field_label.dart';
import 'profile_field_style.dart';

const BorderRadius _radius4 =
    BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

/// The read-only date-of-birth field: tapping it runs [onTap] (the screen's
/// date picker). [displayValue] is the already-formatted date (or '—'); the
/// screen owns the value and the formatting.
class DateOfBirthField extends StatelessWidget {
  const DateOfBirthField({
    required this.displayValue,
    required this.hasError,
    required this.onTap,
    super.key,
  });

  final String displayValue;
  final bool hasError;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        FieldLabel(l10n.dateOfBirthLabel),
        const SizedBox(height: 8),
        InkWell(
          onTap: onTap,
          borderRadius: _radius4,
          child: InputDecorator(
            decoration: profileFieldDecoration(
              errorText: hasError ? l10n.dateOfBirthRequired : null,
              suffixIcon: const Icon(
                Icons.calendar_today_outlined,
                color: SimfTokens.greyText,
                size: 18,
              ),
            ),
            child: Text(displayValue, style: profileInputStyle),
          ),
        ),
      ],
    );
  }
}
