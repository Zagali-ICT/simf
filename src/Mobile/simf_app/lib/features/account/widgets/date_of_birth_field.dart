import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';

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
        SimfFieldLabel(l10n.dateOfBirthLabel),
        const SizedBox(height: SimfTokens.space2),
        InkWell(
          onTap: onTap,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: InputDecorator(
            decoration: simfFieldDecoration(
              errorText: hasError ? l10n.dateOfBirthRequired : null,
              suffixIcon: const Icon(
                Icons.calendar_today_outlined,
                color: SimfTokens.greyText,
                size: SimfTokens.dateOfBirthFieldSize,
              ),
            ),
            child: Text(displayValue, style: simfInputStyle),
          ),
        ),
      ],
    );
  }
}
