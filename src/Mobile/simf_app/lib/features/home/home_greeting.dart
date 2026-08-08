import 'package:simf_app/app/localization/app_l10n.dart';

/// The greeting word by local time of day (the frame's "صباح الخير" row).
String homeGreeting(AppL10n l10n, DateTime now) =>
    now.hour < 12 ? l10n.greetingMorning : l10n.greetingEvening;

/// The relative "time-ago" label for the latest-post card (the frame's
/// "قبل ساعة"). Buckets: just-now → minutes → hours → days.
String homePostTime(AppL10n l10n, DateTime published, DateTime nowUtc) {
  final diff = nowUtc.difference(published);
  if (diff.inMinutes < 1) {
    return l10n.postTimeJustNow;
  }
  if (diff.inHours < 1) {
    return l10n.postTimeMinutesAgo(diff.inMinutes);
  }
  if (diff.inHours < 24) {
    return l10n.postTimeHoursAgo(diff.inHours);
  }
  return l10n.postTimeDaysAgo(diff.inDays);
}
