import 'package:meta/meta.dart';

import 'app_role.dart';
import 'preferred_language.dart';
import 'registration_status.dart';

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
  /// screens; the screen-level gate uses [appRole.isAtLeast] for the
  /// permission check and [registrationStatus] for the route decision.
  bool get isApproved => registrationStatus == RegistrationStatus.approved;

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
