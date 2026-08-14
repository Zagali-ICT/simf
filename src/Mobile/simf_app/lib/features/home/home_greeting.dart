import 'package:simf_app/app/localization/app_l10n.dart';

/// The name to show in the welcome header.
///
/// A visitor is greeted by a short name, everyone else by their full one: an
/// exhibitor or a sponsor is greeted as the organisation they represent, and
/// clipping that reads as a mistake.
///
/// The short form is the **first two words**, not the first. Taking one token
/// broke every Arabic compound given name — عبد الله greeted as عبد, عبد الرحمن
/// as عبد — which is why the earlier single-token rule was reverted (OA-D1).
/// Two words cannot split a compound, need no prefix list to maintain, and
/// cannot fail silently on a name nobody thought of; the worst case is a
/// slightly longer greeting rather than a mangled name.
String greetingDisplayName(String fullName, {required bool isVisitor}) {
  final trimmed = fullName.trim();
  if (!isVisitor || trimmed.isEmpty) {
    return trimmed;
  }
  final words = trimmed.split(RegExp(r'\s+'));
  return words.length <= 2 ? trimmed : '${words[0]} ${words[1]}';
}

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
