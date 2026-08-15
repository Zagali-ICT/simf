import 'package:meta/meta.dart';

import 'package:simf_auth_pkg/src/domain/app_role.dart';
import 'package:simf_auth_pkg/src/domain/current_user.dart';
import 'package:simf_auth_pkg/src/domain/preferred_language.dart';
import 'package:simf_auth_pkg/src/domain/registration_status.dart';

/// Wire shape for the `user` object returned inside `/auth/sign-in` and
/// `/users/me` (SIMF-MOB-API-001 §5.1).
@immutable
class CurrentUserDto {
  const CurrentUserDto({
    required this.id,
    required this.email,
    required this.displayName,
    required this.appRole,
    required this.preferredLanguage,
    required this.registrationStatus,
    this.avatarUrl,
    this.profileComplete = false,
  });

  factory CurrentUserDto.fromJson(Map<String, dynamic> json) {
    return CurrentUserDto(
      id: json['id'] as String? ?? '',
      email: json['email'] as String? ?? '',
      displayName: json['displayName'] as String? ?? '',
      appRole: json['appRole'],
      preferredLanguage: json['preferredLanguage'],
      registrationStatus: json['registrationStatus'],
      avatarUrl: json['avatarUrl'] as String?,
      // D-374 — server-computed; absent on old payloads → false (the app
      // then routes to the profile form, the safe direction).
      profileComplete: json['profileComplete'] as bool? ?? false,
    );
  }

  final String id;
  final String email;
  final String displayName;
  // These three are kept as `Object?` on the wire — the [toDomain] mapper
  // turns them into enum cases. This way `CurrentUserDto` accepts both
  // string and int forms from the backend without changes.
  final Object? appRole;
  final Object? preferredLanguage;
  final Object? registrationStatus;
  final String? avatarUrl;

  /// D-374 — server-computed profile completeness from `/app/users/me`.
  final bool profileComplete;

  CurrentUser toDomain() {
    return CurrentUser(
      id: id,
      email: email,
      displayName: displayName,
      appRole: AppRole.fromJson(appRole),
      preferredLanguage: PreferredLanguage.fromJson(preferredLanguage),
      registrationStatus: RegistrationStatus.fromJson(registrationStatus),
      avatarUrl: avatarUrl,
      profileComplete: profileComplete,
    );
  }
}
