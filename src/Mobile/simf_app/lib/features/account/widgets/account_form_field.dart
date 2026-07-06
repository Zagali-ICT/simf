import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../../../core/widgets/simf_field_label.dart';
import '../../../core/widgets/simf_field_style.dart';

/// The auth forms' email field: a [SimfFieldLabel] over an LTR [TextFormField]
/// on the shared input style. Shared by sign-in / sign-up so the label,
/// direction and decoration stay in one place.
class AccountEmailField extends StatelessWidget {
  const AccountEmailField({
    required this.controller,
    required this.label,
    required this.enabled,
    this.validator,
    this.onChanged,
    this.maxLength = 50,
    super.key,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final int maxLength;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label, color: SimfTokens.navy),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          keyboardType: TextInputType.emailAddress,
          textDirection: TextDirection.ltr,
          textAlign: TextAlign.start,
          maxLength: maxLength,
          enabled: enabled,
          onChanged: onChanged,
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          decoration: simfFieldDecoration(counterText: ''),
        ),
      ],
    );
  }
}

/// The auth forms' password field: a [SimfFieldLabel] over an obscured
/// [TextFormField] with the show/hide eye toggle. The obscure state is owned by
/// the caller (stateless) so a screen can share one toggle across fields.
class AccountPasswordField extends StatelessWidget {
  const AccountPasswordField({
    required this.controller,
    required this.label,
    required this.obscure,
    required this.onToggleObscure,
    required this.enabled,
    this.validator,
    this.onChanged,
    this.onSubmitted,
    // Matches the server policy max (PasswordPolicy.MaxLength = 128) so a valid
    // existing password longer than the old 32 cap can still be typed.
    this.maxLength = 128,
    super.key,
  });

  final TextEditingController controller;
  final String label;
  final bool obscure;
  final VoidCallback onToggleObscure;
  final bool enabled;
  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final int maxLength;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label, color: SimfTokens.navy),
        const SizedBox(height: 8),
        TextFormField(
          controller: controller,
          obscureText: obscure,
          maxLength: maxLength,
          enabled: enabled,
          onChanged: onChanged,
          onFieldSubmitted: onSubmitted,
          style: simfInputStyle,
          autovalidateMode: AutovalidateMode.onUserInteraction,
          validator: validator,
          decoration: simfFieldDecoration(
            counterText: '',
            suffixIcon: IconButton(
              tooltip: obscure
                  ? l10n.showPasswordTooltip
                  : l10n.hidePasswordTooltip,
              icon: SimfSvgIcon(
                obscure ? AppAssets.authEyeOff : AppAssets.authEye,
                size: 16,
                color: SimfTokens.greyText,
              ),
              onPressed: onToggleObscure,
            ),
          ),
        ),
      ],
    );
  }
}
