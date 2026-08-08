import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/validation/digit_normalization.dart';
import '../../../core/validation/field_limits.dart';
import '../../../core/validation/plate_validation.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';
import '../../../core/widgets/simf_picker_field.dart';

/// C6 (D-371/D-459) — رقم اللوحة, optional. Three letter pickers over the 17
/// official Saudi plate letters (shown "Arabic · Latin") plus a 1–4 digit field.
///
/// The row is forced LTR because a plate reads left-to-right in both locales;
/// the digit box is fixed-width so the three letter pickers (Expanded) absorb
/// the remaining width and can show the picked letter.
///
/// Presentation only: the screen owns the controllers and re-assembles the plate
/// from the picks. Extracted from `sign_up_visitor_screen`.
class PlateNumberField extends StatelessWidget {
  const PlateNumberField({
    required this.l10n,
    required this.letter1,
    required this.letter2,
    required this.letter3,
    required this.digits,
    required this.onPickLetter,
    required this.onDigitsChanged,
    required this.validateDigits,
    super.key,
  });

  final AppL10n l10n;
  final String? letter1;
  final String? letter2;
  final String? letter3;
  final TextEditingController digits;

  /// Opens the shared searchable letter sheet for position 1–3.
  final ValueChanged<int> onPickLetter;

  final VoidCallback onDigitsChanged;
  final FormFieldValidator<String> validateDigits;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.plateNumberLabel),
        const SizedBox(height: SimfTokens.space2),
        Row(
          textDirection: TextDirection.ltr,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(child: _picker(1, letter1)),
            const SizedBox(width: SimfTokens.space2),
            Expanded(child: _picker(2, letter2)),
            const SizedBox(width: SimfTokens.space2),
            Expanded(child: _picker(3, letter3)),
            const SizedBox(width: SimfTokens.space2),
            SizedBox(
              // Sized for exactly the 4 digits, so the three letter pickers
              // (Expanded) absorb the freed width and show the picked letter.
              width: SimfTokens.plateNumberFieldWidth,
              // a11y: name the digit field (its hint vanishes on input).
              child: Semantics(
                label: l10n.plateDigitsLabel,
                textField: true,
                child: TextFormField(
                  controller: digits,
                  textDirection: TextDirection.ltr,
                  maxLength: FieldLimits.plateDigits,
                  keyboardType: TextInputType.number,
                  inputFormatters: <TextInputFormatter>[
                    const WesternDigitsFormatter(),
                    FilteringTextInputFormatter.digitsOnly,
                  ],
                  style: simfInputStyle,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  onChanged: (_) => onDigitsChanged(),
                  validator: validateDigits,
                  decoration: simfFieldDecoration(
                    counterText: '',
                    hintText: l10n.plateDigitsHint,
                  ),
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  /// One letter picker. [position] (1–3) gives each field a distinct accessible
  /// name ("Letter 1/2/3") so a screen reader can tell them apart.
  Widget _picker(int position, String? code) {
    final letter = code == null ? null : plateLetterByCode(code);
    return Semantics(
      label: '${l10n.plateLetterHint} $position',
      child: SimfPickerField(
        fieldKey: 'plateLetter$position',
        displayText: letter == null
            ? l10n.plateLetterHint
            : '${letter.arabic} · ${letter.english}',
        isPlaceholder: letter == null,
        onTap: () => onPickLetter(position),
        showChevron: false,
      ),
    );
  }
}
