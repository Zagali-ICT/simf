import 'package:flutter/services.dart';

/// Folds Arabic-Indic (٠–٩, U+0660–U+0669) and Extended Arabic-Indic / Persian
/// (۰–۹, U+06F0–U+06F9) digits to Western ASCII (0–9); every other character is
/// left untouched. The whole system stores and checks numbers in Western digits
/// — the national-id / iqama shapes, the Luhn check and the phone shapes all
/// only match `[0-9]`, on the client and the server — so a number typed on an
/// Arabic keyboard must fold before it is validated or sent.
String toWesternDigits(String value) {
  final buffer = StringBuffer();
  for (final rune in value.runes) {
    if (rune >= 0x0660 && rune <= 0x0669) {
      buffer.writeCharCode(0x30 + (rune - 0x0660));
    } else if (rune >= 0x06F0 && rune <= 0x06F9) {
      buffer.writeCharCode(0x30 + (rune - 0x06F0));
    } else {
      buffer.writeCharCode(rune);
    }
  }
  return buffer.toString();
}

/// Rewrites Arabic-Indic / Persian digits to Western as the user types, so the
/// field both shows and submits Western digits. Place it before any digits-only
/// filter so an Arabic digit converts instead of being dropped. Each digit is a
/// single UTF-16 unit, so folding preserves length and the caret position maps
/// straight across.
class WesternDigitsFormatter extends TextInputFormatter {
  const WesternDigitsFormatter();

  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final folded = toWesternDigits(newValue.text);
    if (folded == newValue.text) {
      return newValue;
    }
    return TextEditingValue(
      text: folded,
      selection: newValue.selection,
    );
  }
}

/// A phone-number formatter (owner 2026-07-06): the value is all digits with
/// only an optional leading `+` — no letters or other symbols. Arabic-Indic
/// digits fold to Western first, then everything except `0-9` (and a single `+`
/// at position 0) is dropped, keeping the caret in place.
class PhoneNumberFormatter extends TextInputFormatter {
  const PhoneNumberFormatter();

  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final source = toWesternDigits(newValue.text);
    final buffer = StringBuffer();
    var removedBeforeCaret = 0;
    final caret = newValue.selection.end;
    for (var i = 0; i < source.length; i++) {
      final unit = source.codeUnitAt(i);
      final isDigit = unit >= 0x30 && unit <= 0x39;
      final isLeadingPlus = unit == 0x2B && buffer.isEmpty && i == 0;
      if (isDigit || isLeadingPlus) {
        buffer.writeCharCode(unit);
      } else if (i < caret) {
        removedBeforeCaret++;
      }
    }
    final text = buffer.toString();
    if (text == newValue.text) {
      return newValue;
    }
    final offset = (caret - removedBeforeCaret).clamp(0, text.length);
    return TextEditingValue(
      text: text,
      selection: TextSelection.collapsed(offset: offset),
    );
  }
}
