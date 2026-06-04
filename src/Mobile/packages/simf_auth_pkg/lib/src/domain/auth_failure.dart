import 'package:meta/meta.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The auth feature's domain-typed failure.
///
/// The repository layer translates an [ApiFailure] into one of these so
/// screens (and tests) can switch on the case rather than string-compare
/// the error code. The original [ApiFailure] is kept for cases where the
/// caller needs the field-level details (`details` from `VALIDATION_FAILED`)
/// or the human-readable message.
@immutable
sealed class AuthFailure {
  const AuthFailure(this.source);

  /// The raw `ApiFailure` from the data package. Use for `.message` and
  /// `.details` when displaying.
  final ApiFailure source;
}

class InvalidCredentials extends AuthFailure {
  const InvalidCredentials(super.source);
}

class EmailNotVerified extends AuthFailure {
  const EmailNotVerified(super.source);
}

class AccountNotApproved extends AuthFailure {
  const AccountNotApproved(super.source);
}

class AccountDisabled extends AuthFailure {
  const AccountDisabled(super.source);
}

class EmailAlreadyRegistered extends AuthFailure {
  const EmailAlreadyRegistered(super.source);
}

class VerificationCodeInvalid extends AuthFailure {
  const VerificationCodeInvalid(super.source);
}

class VerificationCodeExpired extends AuthFailure {
  const VerificationCodeExpired(super.source);
}

class ResetCodeInvalid extends AuthFailure {
  const ResetCodeInvalid(super.source);
}

class ResetCodeExpired extends AuthFailure {
  const ResetCodeExpired(super.source);
}

class MfaRequired extends AuthFailure {
  const MfaRequired(super.source, this.mfaToken);
  final String mfaToken;
}

class MfaTokenInvalid extends AuthFailure {
  const MfaTokenInvalid(super.source);
}

class MfaTokenExpired extends AuthFailure {
  const MfaTokenExpired(super.source);
}

class TotpInvalid extends AuthFailure {
  const TotpInvalid(super.source);
}

class RefreshTokenInvalid extends AuthFailure {
  const RefreshTokenInvalid(super.source);
}

class RefreshTokenExpired extends AuthFailure {
  const RefreshTokenExpired(super.source);
}

class ValidationFailed extends AuthFailure {
  const ValidationFailed(super.source);
  List<ApiResultErrorDetail> get fieldErrors => source.details;
}

class RateLimited extends AuthFailure {
  const RateLimited(super.source);
}

class NetworkUnavailable extends AuthFailure {
  const NetworkUnavailable(super.source);
}

class UnknownAuthFailure extends AuthFailure {
  const UnknownAuthFailure(super.source);
}

/// Maps a raw [ApiFailure] from the data package to the matching
/// [AuthFailure] case. Centralised so every auth endpoint translates errors
/// the same way.
AuthFailure mapAuthFailure(ApiFailure failure) {
  switch (failure.code) {
    case ApiErrorCodes.authInvalidCredentials:
      return InvalidCredentials(failure);
    case ApiErrorCodes.authEmailNotVerified:
      return EmailNotVerified(failure);
    case ApiErrorCodes.authAccountNotApproved:
      return AccountNotApproved(failure);
    case ApiErrorCodes.authAccountDisabled:
      return AccountDisabled(failure);
    case ApiErrorCodes.authEmailAlreadyRegistered:
      return EmailAlreadyRegistered(failure);
    case ApiErrorCodes.authCodeInvalid:
      return VerificationCodeInvalid(failure);
    case ApiErrorCodes.authCodeExpired:
      return VerificationCodeExpired(failure);
    case ApiErrorCodes.authResetCodeInvalid:
      return ResetCodeInvalid(failure);
    case ApiErrorCodes.authResetCodeExpired:
      return ResetCodeExpired(failure);
    case ApiErrorCodes.authMfaTokenInvalid:
      return MfaTokenInvalid(failure);
    case ApiErrorCodes.authMfaTokenExpired:
      return MfaTokenExpired(failure);
    case ApiErrorCodes.authTotpInvalid:
      return TotpInvalid(failure);
    case ApiErrorCodes.authRefreshTokenInvalid:
      return RefreshTokenInvalid(failure);
    case ApiErrorCodes.authRefreshTokenExpired:
      return RefreshTokenExpired(failure);
    case ApiErrorCodes.validationFailed:
      return ValidationFailed(failure);
    case ApiErrorCodes.rateLimitExceeded:
      return RateLimited(failure);
    case ApiErrorCodes.clientNetwork:
    case ApiErrorCodes.clientTimeout:
      return NetworkUnavailable(failure);
    default:
      return UnknownAuthFailure(failure);
  }
}
