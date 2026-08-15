import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';
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

/// The mobile-number field. The label, keyboard and [validator] switch on
/// [saudi]; the screen owns the controllers and validators and passes the
/// right ones in.
class MobileField extends StatelessWidget {
  const MobileField({
    required this.saudi,
    required this.controller,
    required this.validator,
    super.key,
  });

  final bool saudi;
  final TextEditingController controller;
  final FormFieldValidator<String> validator;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(
          saudi ? l10n.saudiMobileLabel : l10n.internationalMobileLabel,
        ),
        const SizedBox(height: SimfTokens.space2),
        TextFormField(
          controller: controller,
          keyboardType: TextInputType.phone,
          // The number renders left-to-right (leading `+`, digits) but sits at
          // the field's start — the right edge in the RTL form, matching the
          // label above it (owner 2026-07-06).
          textDirection: TextDirection.ltr,
          textAlign: TextAlign.end,
          // Digits only, with an optional leading `+` — no letters or symbols;
          // Arabic-Indic digits fold to Western (owner 2026-07-06).
          inputFormatters: const <TextInputFormatter>[PhoneNumberFormatter()],
          // Covers Saudi 05XXXXXXXX / +9665XXXXXXXX / 009665XXXXXXXX and
          // international +[1-9]\d{7,14} / 00[1-9]\d{7,14}.
          maxLength: FieldLimits.phone,
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          decoration: simfFieldDecoration(counterText: ''),
        ),
      ],
    );
  }
}
