import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/validation/phone_country_code.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
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

/// The mobile-number field: a calling-code selector, then the number.
///
/// **The calling code is independent of nationality.** It used to be derived
/// from it — `saudi: isSaudi` chose both the label and the validator — so a
/// visitor of one nationality attending on another country's number had no way
/// to say so, and no prefix was shown at all before typing. A Sudanese
/// national on a Saudi number is the reported case. The code now defaults from
/// the nationality and is then the visitor's to change.
///
/// The two halves are edited separately and stored as one canonical E.164
/// string, so the screen owns the split (`splitPhone` / `composePhone`).
class MobileField extends StatelessWidget {
  const MobileField({
    required this.saudi,
    required this.controller,
    required this.validator,
    this.callingCode = '',
    this.countries = const <CountryItem>[],
    this.onCallingCodeChanged,
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

  /// The country list the code comes from. Rows without a `phonePrefix` are
  /// skipped rather than offered as a blank choice.
  final List<CountryItem> countries;

  /// Null on the surfaces that have no country list to offer (the My-Area
  /// mobile edit), where the field renders exactly as it did before.
  final ValueChanged<String>? onCallingCodeChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Distinct codes only: several countries share one (+1, +7), and the
    // picker is choosing a CODE, not a country.
    final codes = <String>{
      for (final country in countries)
        if ((country.phonePrefix ?? '').trim().isNotEmpty)
          country.phonePrefix!.trim(),
      // Whatever is currently set stays selectable even if the list does not
      // carry it — otherwise a stored number's code would vanish from its own
      // dropdown and reset itself on first build.
      if (callingCode.trim().isNotEmpty) callingCode.trim(),
    }.toList()
      ..sort();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(
          saudi ? l10n.saudiMobileLabel : l10n.internationalMobileLabel,
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          textDirection: TextDirection.ltr,
          children: <Widget>[
            // The code sits to the LEFT of the number in both languages: a
            // phone number reads left-to-right everywhere, and mirroring it in
            // RTL would put the country code after the subscriber digits.
            // No codes to offer means no selector at all, so a surface with no
            // country list (the My-Area mobile edit) renders exactly the field
            // it rendered before rather than an empty dropdown.
            if (codes.isNotEmpty) ...<Widget>[
              SizedBox(
                width: SimfTokens.mobileCallingCodeWidth,
                child: DropdownButtonFormField<String>(
                  key: const ValueKey<String>('mobileCallingCode'),
                  initialValue:
                      codes.contains(callingCode) ? callingCode : null,
                  isExpanded: true,
                  dropdownColor: SimfTokens.navyDeep,
                  style: simfInputStyle,
                  decoration: simfFieldDecoration(),
                  hint: Text(l10n.mobileCallingCodeHint, style: simfInputStyle),
                  items: <DropdownMenuItem<String>>[
                    for (final code in codes)
                      DropdownMenuItem<String>(
                        value: code,
                        child: Text(
                          code,
                          style: simfInputStyle,
                          textDirection: TextDirection.ltr,
                        ),
                      ),
                  ],
                  onChanged: (value) =>
                      onCallingCodeChanged?.call(value ?? callingCode),
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
