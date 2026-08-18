/// Gregorian weekday names indexed by `DateTime.weekday - 1` (Monday = 0 …
/// Sunday = 6), rendered without an intl locale so no
/// `initializeDateFormatting` call is required. Shared by the date pickers that
/// show an Arabic weekday — e.g. the meeting-request day cards ("الخميس 10
/// يوليو", Figma 1776:4958).
const List<String> _weekdaysAr = <String>[
  'الاثنين',
  'الثلاثاء',
  'الأربعاء',
  'الخميس',
  'الجمعة',
  'السبت',
  'الأحد',
];
const List<String> _weekdaysEn = <String>[
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

String gregorianWeekdayName(DateTime date, {required bool isArabic}) =>
    (isArabic ? _weekdaysAr : _weekdaysEn)[date.weekday - 1];
