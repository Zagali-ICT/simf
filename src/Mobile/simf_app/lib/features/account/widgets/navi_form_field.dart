import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';

/// An on-navy labeled form field — a white [SimfFieldLabel] over a
/// [TextFormField] on [simfInputStyleOnNavy] + [simfFieldDecoration]. The navy
/// counterpart of the light-surface `AccountEmailField`/`AccountPasswordField`,
/// so the navy forgot/reset screens share one field (owner: "create NaviFormField
/// … works with email and password … reuse in reset", D-674) instead of each
/// hand-rolling the label + field + decoration.
///
/// Follows the ambient text direction by default, so an email caret starts at
/// the right under Arabic (like the sign-in field). Pass [textDirection] to pin
/// it — the numeric reset code stays LTR. Set [obscureText] for a password;
/// pass a [suffixIcon] (the mail glyph, or a `NavyPasswordToggle`).
class NaviFormField extends StatelessWidget {
  const NaviFormField({
    required this.label,
    required this.controller,
    this.enabled = true,
    this.obscureText = false,
    this.keyboardType,
    this.maxLength,
    this.hintText,
    this.suffixIcon,
    this.textDirection,
    this.inputFormatters,
    this.validator,
    this.onChanged,
    this.onFieldSubmitted,
    this.autovalidateMode = AutovalidateMode.onUserInteraction,
    super.key,
  });

  final String label;
  final TextEditingController controller;
  final bool enabled;
  final bool obscureText;
  final TextInputType? keyboardType;
  final int? maxLength;
  final String? hintText;

  /// The inline-start glyph (renders left under RTL) — a mail icon or a
  /// `NavyPasswordToggle`.
  final Widget? suffixIcon;

  /// Pins the field's direction; null follows the ambient [Directionality].
  final TextDirection? textDirection;
  final List<TextInputFormatter>? inputFormatters;
  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onFieldSubmitted;

  /// When validation errors appear. Defaults to
  /// [AutovalidateMode.onUserInteraction], the behaviour the forgot/reset
  /// screens were built against.
  ///
  /// The badge screens pass [AutovalidateMode.disabled] because they validate
  /// on submit only: adopting this field there must not start surfacing errors
  /// mid- typing, which would be a UX change dressed up as a refactor.
  final AutovalidateMode autovalidateMode;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label, color: SimfTokens.surface),
        const SizedBox(height: SimfTokens.space2),
        TextFormField(
          controller: controller,
          enabled: enabled,
          obscureText: obscureText,
          keyboardType: keyboardType,
          maxLength: maxLength,
          textDirection: textDirection,
          inputFormatters: inputFormatters,
          autovalidateMode: autovalidateMode,
          validator: validator,
          onChanged: onChanged,
          onFieldSubmitted: onFieldSubmitted,
          style: simfInputStyleOnNavy,
          decoration: simfFieldDecoration(
            counterText: '',
            hintText: hintText,
            suffixIcon: suffixIcon,
          ),
        ),
      ],
    );
  }
}
