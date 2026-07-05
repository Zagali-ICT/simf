/// Gregorian month names indexed by month-1, rendered without an intl locale so
/// no `initializeDateFormatting` call is required. Shared by the card date lines
/// that show an Arabic month — saved-sessions "12 يناير 2026" (1701:8928) and
/// my-meetings "12 يناير – 10:30 PM" (1701:9406).
const List<String> _monthsAr = <String>[
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
];
const List<String> _monthsEn = <String>[
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/// The Gregorian month name for [month] (1–12) in the active locale. Out-of-range
/// input is clamped so a bad date never throws on a card.
String gregorianMonthName(int month, bool isArabic) {
  final index = (month - 1).clamp(0, 11);
  return isArabic ? _monthsAr[index] : _monthsEn[index];
}
