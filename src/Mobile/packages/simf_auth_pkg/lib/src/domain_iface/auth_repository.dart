import '../domain/current_user.dart';
import '../domain/session.dart';

/// The auth feature's repository interface.
///
/// Screens depend on this; the implementation in `data/` is the one that
/// reaches the backend. A fake implementation drives unit tests for the
/// `AuthController`.
abstract class AuthRepository {
  /// Sign in with email + password.
  ///
  /// Returns either a [Session] (standard user) or a [SignInChallenge]
  /// (admin user, TOTP required). Throws an `AuthFailure` on failure
  /// (via the mapper in `domain/auth_failure.dart`).
  Future<SignInResult> signIn({
    required String email,
    required String password,
  });

  /// Complete the TOTP challenge after [signIn] returned [SignInChallenge].
  Future<Session> verifyTotp({
    required String mfaToken,
    required String code,
  });

  /// Start an account: creates the account in a pending, email-unverified
  /// state and sends a six-digit verification code to the email.
  Future<void> signUp({
    required String email,
    required String password,
    required String confirmPassword,
  });

  /// Verify the email with the six-digit code.
  Future<void> verifyEmail({
    required String email,
    required String code,
  });

  /// Re-issue a verification code for a pending account.
  Future<void> resendCode({required String email});

  /// Refresh the access token. Returns the new session. The auth-refresh
  /// dio interceptor calls this on a 401 (via the `AuthTokenSource`
  /// implementation that wraps the controller).
  Future<Session> refresh({required String refreshToken});

  /// Sign out the current session.
  Future<void> signOut({required String refreshToken});

  /// Fetch the current user. Used after a cold start when only the
  /// refresh token was persisted, and after refresh to pick up
  /// role / registration-status changes.
  Future<CurrentUser> getCurrentUser();

  /// Forgot password (request a reset code by email).
  Future<void> forgotPassword({required String email});

  /// Reset password using the emailed reset code.
  Future<void> resetPassword({
    required String email,
    required String code,
    required String newPassword,
    required String confirmPassword,
  });
}

/// A successful sign-in either yields a [Session] outright, or a TOTP
/// challenge for an administrative account. The auth UI branches on this.
sealed class SignInResult {
  const SignInResult();
}

class SignInSession extends SignInResult {
  const SignInSession(this.session);
  final Session session;
}

class SignInChallenge extends SignInResult {
  const SignInChallenge(this.mfaToken);
  final String mfaToken;
}
