/// Full-name shape rules — the client mirror of
/// `UpsertUserProfileRequestValidator`:
/// the Arabic name is Arabic letters only, the English name Latin letters only,
/// and a "full name" is at least 2 whitespace-separated parts (in that one
/// script) — owner 2026-07-07, D-683 (supersedes the D-674 ≥4 rule). Pure
/// predicates only — the screen owns the per-script l10n messages and the order
/// the checks run in.
library;

/// The Arabic name character class, mirroring the server's
/// `UpsertUserProfileRequestValidator.ArabicNameShape`: Arabic letters and
/// tatweel (U+0640), the tashkeel marks U+064B-U+0652 (BUG-021 - an ordinary
/// name carries a SHADDA U+0651, and the old U+0621-U+064A class made the
/// field silently swallow it as the user typed), and whitespace. Arabic-Indic
/// digits, Latin letters and punctuation stay out.
final RegExp arabicNameCharacters = RegExp(r'[\u0621-\u0652\s]');

/// Arabic letters + whitespace only (the whole string).
final RegExp arabicNameLettersOnly = RegExp(r'^[\u0621-\u0652\s]+$');

/// Latin letters + whitespace only (the whole string).
final RegExp englishNameLettersOnly = RegExp(r'^[A-Za-z\s]+$');

/// True when [value] is made up only of letters from [lettersOnly] (Arabic or
/// Latin) plus whitespace. The caller trims before calling.
bool isNameLettersOnly(String value, RegExp lettersOnly) =>
    lettersOnly.hasMatch(value);

/// True when [value] is at least 2 whitespace-separated parts (owner: a full
/// name needs at least two parts, D-683). No upper cap — an Arabic name can
/// carry more, and length is already bounded by the field's maxLength. The
/// caller trims and has already confirmed the value is non-empty +
/// letters-only.
bool hasFullNameParts(String value) {
  final parts = value.split(RegExp(r'\s+')).length;
  return parts >= 2;
}
