import 'package:meta/meta.dart';

import '../../domain/app_role.dart';
import '../../domain/current_user.dart';
import '../../domain/preferred_language.dart';
import '../../domain/registration_status.dart';

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

  CurrentUser toDomain() {
    return CurrentUser(
      id: id,
      email: email,
      displayName: displayName,
      appRole: AppRole.fromJson(appRole),
      preferredLanguage: PreferredLanguage.fromJson(preferredLanguage),
      registrationStatus: RegistrationStatus.fromJson(registrationStatus),
      avatarUrl: avatarUrl,
    );
  }
}
