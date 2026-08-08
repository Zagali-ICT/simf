/// C4 (D-371) — the standard phone shapes, shared by the profile form and
/// its tests. Saudi mobile `05XXXXXXXX` or `+9665XXXXXXXX`; international
/// mobile `+`, non-zero lead, 8–15 digits. Before matching, the value is
/// normalised the same way [normalizePhone] does — separators stripped,
/// Arabic-Indic digits folded to Western, and a leading international `00`
/// rewritten to `+` (owner 2026-07-06) — so `00966…` and an Arabic-typed
/// number validate, and the value submitted to the server is always the
/// `+`-prefixed form the server's shapes require.
library;

import 'package:simf_app/core/validation/digit_normalization.dart';

final RegExp _saudiMobileShape = RegExp(r'^(05\d{8}|\+9665\d{8})$');
final RegExp _internationalShape = RegExp(r'^\+[1-9]\d{7,14}$');

/// The canonical phone value: Arabic-Indic digits folded to Western,
/// spaces / dashes stripped, and a leading `00` international prefix rewritten
/// to `+`. Used both to validate and to produce the value that is submitted.
String normalizePhone(String value) {
  final String stripped =
      toWesternDigits(value.trim()).replaceAll(' ', '').replaceAll('-', '');
  return stripped.startsWith('00') ? '+${stripped.substring(2)}' : stripped;
}

/// True when [value] is a standard Saudi mobile (`05…`, `+9665…` or `009665…`).
bool isStandardSaudiMobile(String value) =>
    _saudiMobileShape.hasMatch(normalizePhone(value));

/// True when [value] is a standard international mobile (`+…` or `00…`).
bool isStandardInternationalMobile(String value) =>
    _internationalShape.hasMatch(normalizePhone(value));
