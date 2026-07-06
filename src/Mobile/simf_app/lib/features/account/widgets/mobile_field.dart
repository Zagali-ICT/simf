import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../core/validation/digit_normalization.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';

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
        const SizedBox(height: 8),
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
          maxLength: 17,
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          decoration: simfFieldDecoration(counterText: ''),
        ),
      ],
    );
  }
}
