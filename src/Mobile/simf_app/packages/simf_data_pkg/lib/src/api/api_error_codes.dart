/// Stable error-code constants from SIMF-API-001 §7 + §12.6, plus the codes
/// the mobile app itself raises when an HTTP error never reached the
/// backend envelope (network failure, malformed JSON, etc).
///
/// Adding a new code here is fine; **renaming or removing one is a wire
/// break** (see SIMF-API-001 §7.2 — "a code, once published, does not
/// change meaning").
class ApiErrorCodes {
  ApiErrorCodes._();

  // Generic
  static const String validationFailed = 'VALIDATION_FAILED';
  static const String rateLimitExceeded = 'RATE_LIMIT_EXCEEDED';
  static const String unknown = 'UNKNOWN_ERROR';

  // Auth — sign-up + verify
  static const String authEmailAlreadyRegistered =
      'AUTH_EMAIL_ALREADY_REGISTERED';
  static const String authAccountNotFound = 'AUTH_ACCOUNT_NOT_FOUND';
  static const String authCodeInvalid = 'AUTH_CODE_INVALID';
  static const String authCodeExpired = 'AUTH_CODE_EXPIRED';

  // Auth — sign-in
  static const String authInvalidCredentials = 'AUTH_INVALID_CREDENTIALS';
  static const String authEmailNotVerified = 'AUTH_EMAIL_NOT_VERIFIED';
  static const String authAccountNotApproved = 'AUTH_ACCOUNT_NOT_APPROVED';
  static const String authAccountDisabled = 'AUTH_ACCOUNT_DISABLED';

  // Auth — TOTP / MFA
  static const String authMfaTokenInvalid = 'AUTH_MFA_TOKEN_INVALID';
  static const String authMfaTokenExpired = 'AUTH_MFA_TOKEN_EXPIRED';
  static const String authTotpInvalid = 'AUTH_TOTP_INVALID';

  // Auth — refresh
  static const String authRefreshTokenInvalid = 'AUTH_REFRESH_TOKEN_INVALID';
  static const String authRefreshTokenExpired = 'AUTH_REFRESH_TOKEN_EXPIRED';

  // Auth — password reset (planned, SIMF-API-001 OI-3)
  static const String authResetCodeInvalid = 'AUTH_RESET_CODE_INVALID';
  static const String authResetCodeExpired = 'AUTH_RESET_CODE_EXPIRED';

  // Client-side synthetic codes — raised when no envelope was received.
  // The wire never sees these; the repository layer maps them from a dio
  // exception so the screen sees one error type regardless of cause.
  static const String clientNetwork = 'CLIENT_NETWORK';
  static const String clientTimeout = 'CLIENT_TIMEOUT';
  static const String clientCancelled = 'CLIENT_CANCELLED';
  static const String clientMalformedResponse = 'CLIENT_MALFORMED_RESPONSE';
}
