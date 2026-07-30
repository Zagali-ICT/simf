import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// The one shared form-field styling for every bordered SIMF input — login,
/// sign-up, profile and any other form. The value text style + the standard
/// [InputDecoration] (beige resting border, gold focus border, dense, no fill).
/// Replaces the former per-feature `authInput*` / `profileField*` copies.

/// Text style for the value typed into a SIMF form field.
const TextStyle simfInputStyle = TextStyle(
  fontSize: 14,
  fontWeight: FontWeight.w500,
  color: SimfTokens.inputInk,
);

/// The value text style for a SIMF field on a navy surface (white text) — the
/// forgot / reset password screens, where [simfInputStyle]'s dark ink would be
/// unreadable against the navy background.
const TextStyle simfInputStyleOnNavy = TextStyle(
  fontSize: 14,
  fontWeight: FontWeight.w500,
  color: SimfTokens.surface,
);

// Radius left at OutlineInputBorder's default (circular 4 ==
// SimfTokens.radiusSmall); passing it trips avoid_redundant_argument_values.
const OutlineInputBorder _restingBorder = OutlineInputBorder(
  borderSide: BorderSide(color: SimfTokens.beigeBorder),
);
const OutlineInputBorder _focusedBorder = OutlineInputBorder(
  borderSide: BorderSide(color: SimfTokens.accent),
);

/// The standard [InputDecoration] for a SIMF form field. Pass [counterText] `''`
/// to hide the maxLength counter (the auth screens' convention).
InputDecoration simfFieldDecoration({
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
    // Let a field error wrap to its full text instead of clipping at one line
    // (Flutter's default errorMaxLines == 1) — the password policy error is a
    // multi-line checklist and every field error must show in full.
    errorMaxLines: 8,
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
