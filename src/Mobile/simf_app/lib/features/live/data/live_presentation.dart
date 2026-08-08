import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../core/organization_profile/organization_profile.dart';
import 'live_models.dart';

/// Presentation rules for the live-broadcast screen, as pure functions.
///
/// These were members of the screen's State but never touched it: each is a
/// function of its arguments alone. Out here they read without the 500-line
/// widget around them and can be asserted directly.

/// Who counts as an ATTENDEE of a live session, for the "rate it after
/// watching" prompt (D-712).
///
/// Deliberately narrower than "signed in": an operational role is working the
/// session, not attending it, and should not be asked to rate it.
bool isAttendeeRole(AppRole role) =>
    role == AppRole.visitor || role == AppRole.exhibitor;

/// The forum-wide broadcast presented as a session, for the case where the
/// stream belongs to the event rather than to a session on the programme.
///
/// `status: 1` is the frozen SessionStatus for "live" on the wire (D-219).
LiveSession globalLiveSession(OrgProfile profile, String url) => LiveSession(
      title: profile.name,
      titleArabic: profile.nameArabic,
      status: 1,
      hasRecording: false,
      liveStreamUrl: url,
    );

/// The frame's header line, "يُبث الآن · {hall}", or just the label when the
/// broadcasting hall is not known.
String broadcastLabel(AppL10n l10n, {required bool isLive, String? hall}) {
  final String base = isLive ? l10n.liveNowBroadcasting : l10n.liveSessionLabel;
  return hall == null ? base : '$base · $hall';
}
