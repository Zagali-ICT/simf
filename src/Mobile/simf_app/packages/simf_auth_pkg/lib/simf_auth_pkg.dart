/// SIMF mobile auth package — sign-in, sign-up, email OTP, refresh,
/// sign-out, forgot / reset password, and get-current-user.
///
/// Depends on `simf_data_pkg` for the single dio client (SIMF-MAA-001 v1.2
/// §9.1, DECISIONS_LOG D-003). Never instantiates `dio.Dio`.
library;

export 'src/application/auth_controller.dart';
export 'src/application/auth_providers.dart';
export 'src/data/auth_api.dart';
export 'src/data/auth_repository_impl.dart';
export 'src/data/dto/current_user_dto.dart';
// The My Devices screen renders DeviceKeyEntryDto rows directly.
export 'src/data/dto/device_key_dtos.dart';
export 'src/data/dto/sign_in_request.dart';
export 'src/data/dto/sign_in_response.dart';
export 'src/data/dto/sign_up_request.dart';
export 'src/data/dto/token_payload_dto.dart';
export 'src/data/dto/verify_email_request.dart';
export 'src/domain/app_role.dart';
export 'src/domain/auth_failure.dart';
export 'src/domain/current_user.dart';
export 'src/domain/preferred_language.dart';
export 'src/domain/registration_status.dart';
export 'src/domain/session.dart';
export 'src/domain_iface/auth_repository.dart';
