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
  });

  final String id;
  final String email;
  final String displayName;
  final AppRole appRole;
  final PreferredLanguage preferredLanguage;
  final RegistrationStatus registrationStatus;
  final String? avatarUrl;

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
  }) {
    return CurrentUser(
      id: id,
      email: email,
      displayName: displayName ?? this.displayName,
      appRole: appRole ?? this.appRole,
      preferredLanguage: preferredLanguage ?? this.preferredLanguage,
      registrationStatus: registrationStatus ?? this.registrationStatus,
      avatarUrl: avatarUrl ?? this.avatarUrl,
    );
  }
}
