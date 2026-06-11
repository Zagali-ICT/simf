import 'package:add_2_calendar/add_2_calendar.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'session_models.dart';

/// The device "add to calendar" action (Page_017 E4) — a **client-local OS
/// action** built entirely from the cached session (no API, works offline).
/// Kept behind an overridable provider so the screen depends on a seam, not the
/// `add_2_calendar` MethodChannel directly (the widget test injects a fake).
class SessionCalendar {
  const SessionCalendar();

  /// Opens the device calendar's add-event editor pre-filled from [detail].
  /// Returns true when the platform reported the event was added.
  Future<bool> addSession(SessionDetail detail, {required bool isArabic}) {
    return Add2Calendar.addEvent2Cal(
      Event(
        title: detail.localizedTitle(isArabic),
        description: detail.localizedDescription(isArabic) ?? '',
        location: detail.localizedHall(isArabic),
        startDate: detail.startLocal,
        endDate: detail.endLocal,
      ),
    );
  }
}

final sessionCalendarProvider =
    Provider<SessionCalendar>((ref) => const SessionCalendar());
