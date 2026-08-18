import 'package:simf_app/features/notifications/data/notification_models.dart';

/// The chips filter by the server `group` code (D-678). The "جلسات" chip covers
/// the event-flow groups; "VIP" covers the VIP group; "الكل" shows everything.
const Set<String> sessionsChipGroups = <String>{
  'Sessions',
  'Bookings',
  'Meetings',
  'Ratings',
};
const Set<String> vipChipGroups = <String>{'Vip'};

enum NotificationFilter { all, sessions, vip }

/// The group for [item]: the server `group`, or a client fallback derived from
/// the kind for rows created before the group column existed.
String groupForItem(NotificationItem item) {
  final group = item.group?.trim();
  if (group != null && group.isNotEmpty) {
    return group;
  }
  switch (item.kind) {
    case 'BookingConfirmed':
    case 'BookingRejected':
      return 'Bookings';
    case 'SessionReminder':
      return 'Sessions';
    case 'MeetingScheduled':
    case 'MeetingCancelled':
    // Bi-Meeting rework — the other-party confirm request + the 15-min
    // reminder.
    case 'MeetingRequested':
    case 'MeetingReminder':
      return 'Meetings';
    case 'InvitationReceived':
    case 'VipBroadcast':
      return 'Vip';
    case 'SessionRatingRequest':
    case 'DayRatingRequest':
    case 'EventRatingRequest':
    case 'AppRatingRequest':
    case 'ExhibitionRatingRequest':
      return 'Ratings';
    default:
      return 'Account';
  }
}
