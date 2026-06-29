import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The shared "login-card" field language for the sign-up / profile form
/// (Figma 168:2972): the value text style + the standard [InputDecoration]
/// (beige resting border, gold focus border, dense, no fill). Used by the
/// screen and every extracted profile field widget so they look identical.

/// Text style for the value typed into a profile form field.
const TextStyle profileInputStyle = TextStyle(
  fontSize: 14,
  fontWeight: FontWeight.w500,
  color: SimfTokens.inputInk,
);

// Radius left at OutlineInputBorder's default (circular 4 ==
// SimfTokens.radiusSmall); passing it trips avoid_redundant_argument_values.
const OutlineInputBorder _restingBorder = OutlineInputBorder(
  borderSide: BorderSide(color: SimfTokens.beigeBorder),
);
const OutlineInputBorder _focusedBorder = OutlineInputBorder(
  borderSide: BorderSide(color: SimfTokens.accent),
);

/// The standard [InputDecoration] for a profile form field.
InputDecoration profileFieldDecoration({
  String? counterText,
  String? hintText,
  String? errorText,
  Widget? prefixIcon,
  Widget? suffixIcon,
}) {
  return InputDecoration(
    counterText: counterText,
    hintText: hintText,
    errorText: errorText,
    prefixIcon: prefixIcon,
    suffixIcon: suffixIcon,
    isDense: true,
    filled: false,
    hintStyle: const TextStyle(color: SimfTokens.greyText),
    contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
    border: _restingBorder,
    enabledBorder: _restingBorder,
    focusedBorder: _focusedBorder,
    disabledBorder: _restingBorder,
  );
}
