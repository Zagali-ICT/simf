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

final RegExp _latinPlate =
    RegExp('^([$_latinClass]{3}[0-9]{1,4}|[0-9]{1,4}[$_latinClass]{3})\$');
final RegExp _arabicPlate =
    RegExp('^([$_arabicClass]{3}[0-9]{1,4}|[0-9]{1,4}[$_arabicClass]{3})\$');

/// Strips separators and folds the input to a canonical comparison form
/// (variant Arabic glyphs normalised, Arabic-Indic digits → Western, Latin
/// upper-cased), preserving character order. Mirrors `SaudiPlate.Prepare`.
String _prepare(String value) {
  final StringBuffer buffer = StringBuffer();
  for (final int rune in value.trim().runes) {
    int c = rune;
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
  final String prepared = _prepare(value);
  return _latinPlate.hasMatch(prepared) || _arabicPlate.hasMatch(prepared);
}

/// The canonical Latin code (Latin letters + Western digits) for [value], or
/// `null` when blank. Assumes the value already passed [isStandardPlateNumber].
String? normalizePlate(String value) {
  if (value.trim().isEmpty) {
    return null;
  }
  final StringBuffer buffer = StringBuffer();
  for (final int rune in _prepare(value).runes) {
    final String ch = String.fromCharCode(rune);
    buffer.write(_arToEn[ch] ?? ch);
  }
  return buffer.toString();
}

/// The Arabic rendering (Arabic letters + Arabic-Indic digits) of [value], or
/// `null` when blank.
String? toArabicPlate(String value) {
  final String? code = normalizePlate(value);
  if (code == null) {
    return null;
  }
  final StringBuffer buffer = StringBuffer();
  for (final int rune in code.runes) {
    final String ch = String.fromCharCode(rune);
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
