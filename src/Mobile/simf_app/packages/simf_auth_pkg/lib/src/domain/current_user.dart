import 'package:meta/meta.dart';

import 'package:simf_auth_pkg/src/domain/app_role.dart';
import 'package:simf_auth_pkg/src/domain/preferred_language.dart';
import 'package:simf_auth_pkg/src/domain/registration_status.dart';

/// The signed-in user as the app sees it.
///
/// Maps from the `CurrentUserDto` returned by `GET /users/me` and embedded
/// inside the sign-in response (SIMF-MOB-API-001 §5.1).
@immutable
class CurrentUser {
  const CurrentUser({
    required this.id,
    required this.email,
    required this.displayName,
    required this.appRole,
    required this.preferredLanguage,
    required this.registrationStatus,
    this.avatarUrl,
    this.profileComplete = false,
  });

  final String id;
  final String email;
  final String displayName;
  final AppRole appRole;
  final PreferredLanguage preferredLanguage;
  final RegistrationStatus registrationStatus;
  final String? avatarUrl;

  /// D-374 — server-computed: the profile carries both names, ≥1 interest
  /// and satisfies the C7 male-photo rule. False until the hydration call
  /// returns, which is the safe default (routes to the profile form).
  final bool profileComplete;

  /// Convenience: is this user allowed past the auth wall? Pending and
  /// rejected accounts can sign in but cannot see Visitor-protected
  /// screens; the screen-level gate uses `appRole.isAtLeast` for the
  /// permission check and [registrationStatus] for the route decision.
  bool get isApproved => registrationStatus == RegistrationStatus.approved;

  /// The role the app UI presents this user as. A signed-in but **not-yet-
  /// approved** account (pending / rejected) has no attendee permissions, so it
  /// presents as [AppRole.guest] regardless of the role its token carries — the
  /// menus, home layout and route role-gate all treat it as a guest. The one
  /// thing it reaches as itself is the registration-status gate, which is
  /// auth-gated (any signed-in user), not role-gated. Once approved this
  /// returns the real [appRole]. Single source of truth for the "pending ⇒
  /// guest" rule.
  AppRole get effectiveAppRole => isApproved ? appRole : AppRole.guest;

  CurrentUser copyWith({
    String? displayName,
    AppRole? appRole,
    PreferredLanguage? preferredLanguage,
    RegistrationStatus? registrationStatus,
    String? avatarUrl,
    bool? profileComplete,
  }) {
    return CurrentUser(
      id: id,
      email: email,
      displayName: displayName ?? this.displayName,
      appRole: appRole ?? this.appRole,
      preferredLanguage: preferredLanguage ?? this.preferredLanguage,
      registrationStatus: registrationStatus ?? this.registrationStatus,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      profileComplete: profileComplete ?? this.profileComplete,
    );
  }
}
