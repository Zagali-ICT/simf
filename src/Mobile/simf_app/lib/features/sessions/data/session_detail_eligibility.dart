import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Who may do what on a session detail, as pure functions of the caller's role
/// and the session itself.
///
/// These were getters on the screen's State, which put the access rules for the
/// join, ask-a-question and check-in affordances inside a 500-line widget.
/// They are decisions, not rendering, and each carries a defect id that
/// explains why it is shaped the way it is.
///
/// Each one asks the ROUTER's own table ([routeAllowsRole], D-519) rather than
/// re-listing roles, so the offer and the route can never disagree.

/// The caller's effective role. A signed-out or not-yet-approved account
/// presents as [AppRole.guest] (D-666).
AppRole roleOf(AuthState auth) => auth is AuthStateSignedIn
    ? auth.session.user.effectiveAppRole
    : AppRole.guest;

/// DEF-MOD-004 - join / my-seat are attendee-only routes (#18 and #109 share
/// the same allowed set), so the UI offers them only to a role that can
/// actually open them.
bool canJoinSession(AppRole role) => routeAllowsRole(RouteNames.mySeat, role);

/// DEF-MOD-003 - the اسأل المحاور card opens the attendee-only send-question
/// route (#26).
///
/// A GUEST (and a pending account, which presents as one) still SEES the card,
/// disabled - that is the existing sign-in nudge. An operational role the
/// router would bounce is not offered it at all.
bool canAskQuestion(AppRole role) =>
    role == AppRole.guest || routeAllowsRole(RouteNames.sendQuestion, role);

/// Whether the moderator's Q&A-desk action is offered in the detail header.
///
/// Moderator-EXCLUSIVE (D-519): Staff no longer inherits it, the focused role
/// model having dropped the isAtLeast ladder. The server still enforces the
/// per-session SessionModerator grant with a 403.
///
/// FR-MOD-001 - the role alone is NOT the gate. The grant is per-session, so
/// the icon used to appear on every session in the programme and a missing
/// grant was only discoverable as a 403 after the tap. A CONFIRMED grant for
/// THIS session is required; while the discovery call is in flight, or if it
/// failed, [moderatedSessionIds] is empty and no action is offered. An icon
/// that 403s is worse than none - the moderator's own home lists their
/// sessions and surfaces the failure there with a retry.
bool canModerateSession(
  AppRole role,
  Set<String> moderatedSessionIds,
  String sessionId,
) =>
    role == AppRole.moderator && moderatedSessionIds.contains(sessionId);

/// D-771 - Staff entry to the seating desk. Staff and Moderator are disjoint
/// focused roles (D-519), so the two never compete for the header's single
/// trailing slot. UX gate only; the server enforces Seating.Assist with a 403.
///
/// Unlike its siblings above this asks the role directly rather than
/// [routeAllowsRole]. That is the shipped behaviour and is preserved verbatim
/// here; whether it should ask the router's table instead is a real question,
/// but it is a behaviour change and does not belong in a structural move.
bool canAssistSeating(AppRole role) => role == AppRole.staff;

/// Whether the hall check-in strip is offered. Three gates, each for its own
/// reason:
///
/// * It reads the CALLER's own attendance from a bearer-gated endpoint, so it
///   follows the same attendee gate as the seat map (D-576/D-577; D-666
///   presents a not-yet-approved account as a guest): a guest has no attendance
///   to report and would only ever see the failed-read state.
/// * A session too far in the future has nothing to report yet. The cut-off is
///   the session's own arrival grace, matching the server, so the two agree.
/// * #29 - a workshop's detail is the title + time block only, so it carries no
///   attendance section either.
bool showArrivalStatus(SessionDetail detail, AppRole role) =>
    canJoinSession(role) &&
    detail.type != SessionType.workshop &&
    !saudiNow().isBefore(detail.start.subtract(detail.arrivalGrace));
