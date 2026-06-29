import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import 'field_label.dart';
import 'profile_field_style.dart';

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
        FieldLabel(
          saudi ? l10n.saudiMobileLabel : l10n.internationalMobileLabel,
        ),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          keyboardType: TextInputType.phone,
          textDirection: TextDirection.ltr,
          style: profileInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          decoration: profileFieldDecoration(),
        ),
      ],
    );
  }
}
