import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/phone_country_code.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';
import 'package:simf_app/core/widgets/simf_picker_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The mobile-number form rule for this field: required (D-723), then the C4
/// (D-371) standard shape — Saudi `05XXXXXXXX` / `+9665XXXXXXXX` or E.164
/// international — mirroring the server's `UpsertUserProfileRequestValidator`.
///
/// This used to claim it was the single home so "no screen writes a second
/// copy", while `visitor_profile/data/visitor_profile_validators.dart` carried
/// the same two rules with the same messages. It now delegates there, and that
/// file is the home; the shapes themselves live in
/// `core/validation/phone_validation.dart` beneath both.
String? validateMobile(
  String? value, {
  required bool saudi,
  required AppL10n l10n,
}) =>
    saudi
        ? validateSaudiMobile(value, l10n)
        : validateInternationalMobile(value, l10n);

/// The mobile-number field: a calling-code picker, then the number.
///
/// **The calling code DEFAULTS from the nationality and is then the visitor's
/// to change.** It used to be derived from it outright — one `saudi` flag chose
/// the label, the validator and the wire field — so a visitor of one
/// nationality attending on another country's number had no way to say so.
///
/// The code uses [SimfPickerField], the same chrome as nationality,
/// birth-region, profile-type and plate-letter, and opens the same searchable
/// country sheet. It was briefly a `DropdownButtonFormField`, which put a
/// control on this form that looked like nothing else on it.
class MobileField extends StatelessWidget {
  const MobileField({
    required this.saudi,
    required this.controller,
    required this.validator,
    this.callingCode = '',
    this.onPickCallingCode,
    super.key,
  });

  /// Still drives the LABEL and the validator, because the server keeps two
  /// wire fields and the Saudi one has its own shape.
  final bool saudi;

  /// The subscriber digits only — the calling code lives in [callingCode].
  final TextEditingController controller;
  final FormFieldValidator<String> validator;

  /// The chosen `+`-prefixed calling code, or empty before one is resolved.
  final String callingCode;

  /// Opens the searchable country sheet. Null on a surface with no country list
  /// to offer (the My-Area mobile edit), where the picker is hidden and the
  /// field renders exactly as it did before.
  final VoidCallback? onPickCallingCode;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final hasCode = callingCode.trim().isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(
          saudi ? l10n.saudiMobileLabel : l10n.internationalMobileLabel,
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          // The code sits to the LEFT of the number in both languages: a phone
          // number reads left-to-right everywhere, and mirroring it in RTL
          // would put the country code after the subscriber digits.
          textDirection: TextDirection.ltr,
          children: <Widget>[
            if (onPickCallingCode != null) ...<Widget>[
              SizedBox(
                width: SimfTokens.mobileCallingCodeWidth,
                child: SimfPickerField(
                  fieldKey: 'mobileCallingCode',
                  displayText:
                      hasCode ? callingCode : l10n.mobileCallingCodeHint,
                  isPlaceholder: !hasCode,
                  onTap: onPickCallingCode,
                  // The code only — the hint is Arabic prose and mirrors.
                  textDirection: hasCode ? TextDirection.ltr : null,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
            ],
            Expanded(
              child: TextFormField(
                controller: controller,
                keyboardType: TextInputType.phone,
                // The number renders left-to-right (digits) but sits at the
                // field's start — the right edge in the RTL form, matching the
                // label above it (owner 2026-07-06).
                textDirection: TextDirection.ltr,
                textAlign: TextAlign.end,
                // Digits only, with an optional leading `+` — no letters or
                // symbols; Arabic-Indic digits fold to Western (owner
                // 2026-07-06).
                inputFormatters: const <TextInputFormatter>[
                  PhoneNumberFormatter(),
                ],
                maxLength: FieldLimits.phone,
                style: simfInputStyle,
                autovalidateMode: AutovalidateMode.onUserInteraction,
                // Validate the WHOLE number, not the box's contents. The box
                // holds subscriber digits once a calling code is chosen, and
                // the rules are written for a complete number — so validating
                // the raw text rejects "501234567" as a malformed Saudi mobile
                // when it is the correct tail of +966501234567.
                validator: (value) => validator(
                  composePhone(callingCode, value ?? ''),
                ),
                decoration: simfFieldDecoration(counterText: ''),
              ),
            ),
          ],
        ),
      ],
    );
  }
}
