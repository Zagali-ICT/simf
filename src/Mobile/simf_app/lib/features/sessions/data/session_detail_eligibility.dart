import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../../app/route_names.dart';
import '../../../app/router.dart';
import '../../../core/utils/saudi_time.dart';
import 'session_models.dart';

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
    role == AppRole.guest ||
    routeAllowsRole(RouteNames.sendQuestion, role);

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
