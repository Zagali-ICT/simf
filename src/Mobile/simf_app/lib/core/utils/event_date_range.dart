import 'package:simf_app/core/utils/gregorian_month_names.dart';

/// Formats the forum's event date range (the CP-editable
/// `OrganizationProfile.EventStartDate` / `EventEndDate`) as one bilingual
/// label, mirroring the server-side `SIMF.Common.EventDateRange` so the app,
/// Website and seeder never disagree. Month names come from the shared
/// [gregorianMonthName]; digits are Western in both languages (matching the app
/// splash and the Figma design).
///
/// Same-month ranges collapse to a single month + year ("23-25 November 2026" /
/// "23-25 نوفمبر 2026"); cross-month and cross-year ranges spell out both
/// endpoints ("30 November - 2 December 2026"). A reversed pair is ordered so the
/// earlier date always reads first; a single-day range renders one date.
String formatEventDateRange(
  DateTime start,
  DateTime end, {
  required bool isArabic,
}) {
  var s = DateTime(start.year, start.month, start.day);
  var e = DateTime(end.year, end.month, end.day);
  if (e.isBefore(s)) {
    final swap = s;
    s = e;
    e = swap;
  }

  String full(DateTime d) =>
      '${d.day} ${gregorianMonthName(d.month, isArabic)} ${d.year}';

  if (s.isAtSameMomentAs(e)) {
    return full(s);
  }
  // Same month and year → one month + year, day range only.
  if (s.year == e.year && s.month == e.month) {
    return '${s.day}-${e.day} '
        '${gregorianMonthName(s.month, isArabic)} ${s.year}';
  }
  // Same year, different month → the year is shown once, at the end.
  if (s.year == e.year) {
    return '${s.day} ${gregorianMonthName(s.month, isArabic)} - ${full(e)}';
  }
  // Different year → both endpoints carry their own year.
  return '${full(s)} - ${full(e)}';
}
