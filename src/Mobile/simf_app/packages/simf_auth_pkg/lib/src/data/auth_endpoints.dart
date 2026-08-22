/// App API routes owned by the auth package.
///
/// Same contract as the app's per-feature `*_endpoints.dart` files (D-545): a
/// route is stated once and is greppable, rather than written at the call site.
/// `AuthApi` holds the request/response shaping; this file holds the paths.
///
/// These paths are the shipped wire contract (D-219). Installed builds call
/// them, so they are copied exactly and must not be reshaped.
abstract final class AuthEndpoints {
  static const String signIn = '/app/auth/sign-in';
  static const String badgeSignIn = '/app/auth/badge-sign-in';
  static const String verifyOtp = '/app/auth/verify-otp';
  static const String resendOtp = '/app/auth/resend-otp';
  static const String signUp = '/app/auth/sign-up';
  static const String verifyEmail = '/app/auth/verify-email';
  static const String resendCode = '/app/auth/resend-code';
  static const String refresh = '/app/auth/refresh';
  static const String signOut = '/app/auth/sign-out';
  static const String currentUser = '/app/users/me';
  static const String signInWithDeviceKey = '/app/auth/sign-in-with-device-key';
  static const String forgotPassword = '/app/auth/forgot-password';
  static const String resetPassword = '/app/auth/reset-password';
  static const String resolveBadge = '/app/auth/resolve-badge';
  static const String badgeActivationStart = '/app/auth/badge-activation/start';
  static const String badgeActivationComplete =
      '/app/auth/badge-activation/complete';

  /// Enrol (POST) and list (GET) the caller's device keys.
  static const String deviceKeys = '/app/auth/device-keys';

  /// Re-assert an existing device key without a full sign-in.
  static const String deviceKeyStepUp = '/app/auth/device-keys/step-up';

  /// The server nonce a device key signs to prove possession.
  static String deviceKeyChallenge(String deviceKeyId) =>
      '/app/auth/device-keys/$deviceKeyId/challenge';

  /// One enrolled device key, by id (DELETE revokes it).
  static String deviceKey(String deviceKeyId) =>
      '/app/auth/device-keys/$deviceKeyId';
}
