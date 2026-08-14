/// C6 (D-459) — the official Saudi vehicle-plate letter set, mirroring the
/// server `SaudiPlate` exactly: the 17 Arabic letters permitted on KSA plates
/// and their single-character Latin codes (the Elm / Absher transliteration).
/// A plate is 3 letters (all from this set, one script) + 1–4 digits, in
/// either order, ≤ 7 chars once separators are stripped.
///
/// The set is a fixed bijection, so a plate is losslessly convertible between
/// scripts. [saudiPlateLetters] is the list the sign-up screen renders in its
/// letter dropdowns; [normalizePlate] / [toArabicPlate] produce the canonical
/// code and the Arabic rendering.
library;

/// One permitted plate letter: its stable Latin [code] (== [english]) plus the
/// [arabic] and [english] glyphs shown in the dropdown.
class SaudiPlateLetter {
  const SaudiPlateLetter(this.code, this.arabic, this.english);
  final String code;
  final String arabic;
  final String english;
}

/// The 17 permitted letters, in the canonical Elm / Absher order.
/// The [SaudiPlateLetter] for a stored Latin [code], or null when the code is
/// not one of the 17 (a pre-D-459 or hand-entered plate).
SaudiPlateLetter? plateLetterByCode(String code) {
  for (final letter in saudiPlateLetters) {
    if (letter.code == code) {
      return letter;
    }
  }
  return null;
}

const List<SaudiPlateLetter> saudiPlateLetters = <SaudiPlateLetter>[
  SaudiPlateLetter('A', 'ا', 'A'),
  SaudiPlateLetter('B', 'ب', 'B'),
  SaudiPlateLetter('J', 'ح', 'J'),
  SaudiPlateLetter('D', 'د', 'D'),
  SaudiPlateLetter('R', 'ر', 'R'),
  SaudiPlateLetter('S', 'س', 'S'),
  SaudiPlateLetter('X', 'ص', 'X'),
  SaudiPlateLetter('T', 'ط', 'T'),
  SaudiPlateLetter('E', 'ع', 'E'),
  SaudiPlateLetter('G', 'ق', 'G'),
  SaudiPlateLetter('K', 'ك', 'K'),
  SaudiPlateLetter('L', 'ل', 'L'),
  SaudiPlateLetter('Z', 'م', 'Z'),
  SaudiPlateLetter('N', 'ن', 'N'),
  SaudiPlateLetter('H', 'ه', 'H'),
  SaudiPlateLetter('U', 'و', 'U'),
  SaudiPlateLetter('V', 'ى', 'V'),
];

final Map<String, String> _arToEn = <String, String>{
  for (final SaudiPlateLetter l in saudiPlateLetters) l.arabic: l.english,
};
final Map<String, String> _enToAr = <String, String>{
  for (final SaudiPlateLetter l in saudiPlateLetters) l.english: l.arabic,
};

const String _latinClass = 'ABDEGHJKLNRSTUVXZ';
const String _arabicClass = 'ابحدرسصطعقكلمنهوى';

// Relaxed rule (owner 2026-07-06): at least one plate letter and/or at least
// one digit — up to 3 letters (from the 17-letter set, one script) and up to 4
// digits, in either order. The `(?=.)` lookahead rejects an all-empty match; a
// non-empty value made only of allowed characters (letters-then-digits or
// digits-then-letters, never interleaved) validates. Mirrored on the server
// (`SaudiPlate`).
final RegExp _latinPlate = RegExp(
  '^(?=.)([$_latinClass]{0,3}[0-9]{0,4}|[0-9]{0,4}[$_latinClass]{0,3})\$',
);
final RegExp _arabicPlate = RegExp(
  '^(?=.)([$_arabicClass]{0,3}[0-9]{0,4}|[0-9]{0,4}[$_arabicClass]{0,3})\$',
);

/// Strips separators and folds the input to a canonical comparison form
/// (variant Arabic glyphs normalised, Arabic-Indic digits → Western, Latin
/// upper-cased), preserving character order. Mirrors `SaudiPlate.Prepare`.
String _prepare(String value) {
  final buffer = StringBuffer();
  for (final rune in value.trim().runes) {
    var c = rune;
    if (c == 0x20 || c == 0x2D || c == 0xA0) {
      continue; // space, dash, non-breaking space
    } else if (c == 0x0623 || c == 0x0625 || c == 0x0622 || c == 0x0671) {
      c = 0x0627; // أ إ آ ٱ → ا
    } else if (c == 0x064A) {
      c = 0x0649; // ي → ى (the V letter)
    } else if (c >= 0x0660 && c <= 0x0669) {
      c = 0x30 + (c - 0x0660); // Arabic-Indic digit → Western
    } else if (c >= 0x61 && c <= 0x7A) {
      c = c - 0x20; // lower-case → upper-case
    }
    buffer.writeCharCode(c);
  }
  return buffer.toString();
}

/// True when [value] is a valid Saudi plate — 3 letters from the 17-letter set
/// (one script) + 1–4 digits, either order (separators ignored).
bool isStandardPlateNumber(String value) {
  if (value.trim().isEmpty) {
    return false;
  }
  final prepared = _prepare(value);
  return _latinPlate.hasMatch(prepared) || _arabicPlate.hasMatch(prepared);
}

/// The canonical Latin code (Latin letters + Western digits) for [value], or
/// `null` when blank. Assumes the value already passed [isStandardPlateNumber].
String? normalizePlate(String value) {
  if (value.trim().isEmpty) {
    return null;
  }
  final buffer = StringBuffer();
  for (final rune in _prepare(value).runes) {
    final ch = String.fromCharCode(rune);
    buffer.write(_arToEn[ch] ?? ch);
  }
  return buffer.toString();
}

/// The Arabic rendering (Arabic letters + Arabic-Indic digits) of [value], or
/// `null` when blank.
String? toArabicPlate(String value) {
  final code = normalizePlate(value);
  if (code == null) {
    return null;
  }
  final buffer = StringBuffer();
  for (final rune in code.runes) {
    final ch = String.fromCharCode(rune);
    if (_enToAr.containsKey(ch)) {
      buffer.write(_enToAr[ch]);
    } else if (rune >= 0x30 && rune <= 0x39) {
      buffer.writeCharCode(0x0660 + (rune - 0x30)); // Western digit → Arabic-Indic
    } else {
      buffer.write(ch);
    }
  }
  return buffer.toString();
}

/// The three letter picks + digits a Saudi plate splits into, for the sign-up
/// form's letter dropdowns + digit field.
class PlateParts {
  const PlateParts({
    this.letter1,
    this.letter2,
    this.letter3,
    this.digits = '',
    this.digitsFirst = false,
    this.rawOverride,
  });

  final String? letter1;
  final String? letter2;
  final String? letter3;
  final String digits;

  /// True when the stored plate led with digits ("1234ABJ"); [assemblePlate]
  /// re-emits in that order so a digits-first plate round-trips (D-471).
  final bool digitsFirst;

  /// Non-null when the code did not parse into 3 dropdown letters — the value
  /// to keep verbatim in the plate field (empty string clears it; a legacy /
  /// non-conforming code is kept so an unrelated edit never erases it, D-468).
  final String? rawOverride;
}

/// Re-assembles a plate string from the dropdown letter picks + digits,
/// preserving [digitsFirst] order. Empty when nothing is set (the plate is
/// optional). The pure form of the sign-up screen's `_syncPlate`.
String assemblePlate({
  required String digits, required bool digitsFirst, String? letter1,
  String? letter2,
  String? letter3,
}) {
  final letters = '${letter1 ?? ''}${letter2 ?? ''}${letter3 ?? ''}';
  final d = digits.trim();
  if (letters.isEmpty && d.isEmpty) {
    return '';
  }
  return digitsFirst ? '$d$letters' : '$letters$d';
}

/// Splits a stored plate [code] into the three dropdown letters + digits (the
/// pure form of the sign-up screen's `_setPlateFromCode`). The value is first
/// normalised to the canonical Latin code, so an Arabic-script or pre-D-459
/// plate still parses; a code the 17-letter dropdowns can't represent comes
/// back as [PlateParts.rawOverride]. Blank → an empty override (clears it).
PlateParts parsePlate(String? code) {
  final raw = code?.trim() ?? '';
  if (raw.isEmpty) {
    return const PlateParts(rawOverride: '');
  }
  final canonical = normalizePlate(raw) ?? raw;
  final letters = <String>[];
  final digits = StringBuffer();
  for (final rune in canonical.runes) {
    if (rune >= 0x30 && rune <= 0x39) {
      digits.writeCharCode(rune);
    } else {
      letters.add(String.fromCharCode(rune).toUpperCase());
    }
  }
  final codes =
      saudiPlateLetters.map((l) => l.code).toSet();
  if (letters.length == 3 && letters.every(codes.contains)) {
    final firstRune = canonical.runes.first;
    return PlateParts(
      letter1: letters[0],
      letter2: letters[1],
      letter3: letters[2],
      digits: digits.toString(),
      digitsFirst: firstRune >= 0x30 && firstRune <= 0x39,
    );
  }
  return PlateParts(rawOverride: canonical);
}
