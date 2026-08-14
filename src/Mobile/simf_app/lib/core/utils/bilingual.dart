/// Picking the active language out of a bilingual pair.
///
/// The API sends most text twice — `title` and `titleArabic`, `name` and
/// `nameArabic` — and every feature's models had its own private copy of the
/// rule for choosing between them: `_pick`, `_pickOpt`, `_pickRequired`,
/// `_pickOptional`, `_picked`. Fourteen copies across twelve files, differing
/// only in name and argument order.
///
/// The rule itself is not obvious enough to be worth re-deriving twelve times:
/// prefer the active language, but fall back to the other when the preferred
/// side is blank, because the CP lets a field be filled in one language only.
library;

/// The active language's value, falling back to the other when the preferred
/// side is blank or whitespace. Returns `''` only when both sides are blank.
///
/// The result is **trimmed**. Twelve of the fourteen copies this replaced
/// trimmed it; `faq_models` and `moderation_models` returned the original
/// string, so those two now strip surrounding whitespace they previously kept.
/// That is the intended behaviour everywhere else, and padding around a
/// user-facing string is a defect rather than a feature.
String pickLocalized(
  String? arabic,
  String? english, {
  required bool isArabic,
}) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

/// [pickLocalized], but `null` rather than `''` when both sides are blank —
/// for the fields a screen hides entirely when there is nothing to show.
String? pickLocalizedOrNull(
  String? arabic,
  String? english, {
  required bool isArabic,
}) {
  final value = pickLocalized(arabic, english, isArabic: isArabic);
  return value.isEmpty ? null : value;
}
