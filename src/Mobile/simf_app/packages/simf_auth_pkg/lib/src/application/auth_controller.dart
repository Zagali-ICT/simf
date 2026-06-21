import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../data/auth_api.dart';
import '../data/auth_repository_impl.dart';
import 'auth_providers.dart';
import '../data/device_key_client.dart';
import '../domain/app_role.dart';
import '../domain/auth_failure.dart';
import '../domain/current_user.dart';
import '../domain/preferred_language.dart';
import '../domain/registration_status.dart';
import '../domain/session.dart';
import '../domain_iface/auth_repository.dart';

/// The state surfaced by the [AuthController]. A screen watches this and
/// renders sign-in vs signed-in views.
sealed class AuthState {
  const AuthState();
}

/// We haven't tried to read the persisted session yet. Render a splash.
class AuthStateInitial extends AuthState {
  const AuthStateInitial();
}

/// No session is active. The router redirects protected routes to sign-in.
class AuthStateSignedOut extends AuthState {
  const AuthStateSignedOut();
}

class AuthStateSignedIn extends AuthState {
  const AuthStateSignedIn(this.session);
  final Session session;
}

/// A sign-in needs the emailed OTP second factor (visitor 2FA). The router
/// redirects to the OTP entry screen. The app has no TOTP path.
class AuthStateAwaitingOtp extends AuthState {
  const AuthStateAwaitingOtp(this.otpToken, {this.email});
  final String otpToken;

  /// The address the code was emailed to — shown on the OTP screen's recipient
  /// line. Null when the sign-in path didn't carry it.
  final String? email;
}

/// The repository the controller consumes. Wired by [authRepositoryProvider]
/// in `auth_providers.dart`. Tests override the provider with a fake.
final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepositoryImpl(api: AuthApi(ref.watch(simfApiClientProvider)));
});

/// Owns the session lifecycle: cold-start restore, sign-in, refresh,
/// sign-out, and the [AuthTokenSource] contract the data package's
/// interceptors rely on.
class AuthController extends Notifier<AuthState> implements AuthTokenSource {
  late final AuthRepository _repository;
  late final SimfSecureStorage _secureStorage;

  // In-memory copies of the tokens so the headers interceptor can read the
  // access token synchronously. Both are kept in sync with secure storage.
  String? _accessToken;
  String? _refreshToken;

  // #9 — "Keep me logged in". When false the session is kept in memory ONLY:
  // it works for this run (refresh uses the in-memory token) but is never
  // written to durable storage, so it does not survive an app restart. Set per
  // sign-in; defaults to remembered (the device-key / cold-start paths).
  bool _rememberSession = true;

  // Single-flight guard for [refresh] — the in-flight refresh Future shared by
  // every concurrent 401 caller. See [refresh].
  Future<bool>? _refreshInFlight;

  final DeviceKeyClient _deviceKeyClient = const DeviceKeyClient();

  @override
  AuthState build() {
    _repository = ref.watch(authRepositoryProvider);
    _secureStorage = ref.watch(simfSecureStorageProvider);
    // D-372 — register this controller as the live token source behind the
    // data package's interceptors. The read goes controller → token source
    // (the acyclic direction); the bridge carries no provider dependencies.
    final tokenSource = ref.read(authTokenSourceProvider);
    if (tokenSource is AuthTokenBridge) {
      tokenSource.delegate = this;
    }
    // Kicks off the cold-start restore asynchronously. The state stays
    // `Initial` until restore finishes, at which point it becomes either
    // `SignedIn` or `SignedOut`.
    unawaited(_restoreFromStorage());
    return const AuthStateInitial();
  }

  // ----- AuthTokenSource ----------------------------------------------------

  @override
  String? currentAccessToken() => _accessToken;

  @override
  Future<bool> refresh() {
    // Single-flight (D-443): when several requests 401 at the same moment —
    // e.g. the home screen's parallel loads right after the 5-min access token
    // lapses — they must share ONE refresh, not each rotate the same refresh
    // token. Without this the second rotation presents an already-rotated token
    // and the server's reuse-detection revokes the whole session, signing an
    // active user out mid-use. Serialising the refresh is the standard
    // OAuth / MSAL guidance for concurrent 401s. The shared Future is cleared
    // when it settles so the next genuine expiry refreshes again.
    return _refreshInFlight ??= _refreshOnce().whenComplete(() {
      _refreshInFlight = null;
    });
  }

  Future<bool> _refreshOnce() async {
    final refreshToken = _refreshToken;
    if (refreshToken == null || refreshToken.isEmpty) {
      return false;
    }
    try {
      final session = await _repository.refresh(refreshToken: refreshToken);
      // The refresh payload's user (AuthUser) carries no app-role; if we are
      // already signed in, keep the resolved privilege and only rotate tokens
      // so a mid-session 401 refresh never downgrades the user to Guest.
      final current = state;
      final merged = current is AuthStateSignedIn
          ? session.copyWith(user: current.session.user)
          : session;
      await _persistSession(merged);
      _setSignedIn(merged);
      return true;
    } on AuthFailure {
      await _clearSessionStorage();
      _accessToken = null;
      _refreshToken = null;
      state = const AuthStateSignedOut();
      return false;
    }
  }

  @override
  Future<void> onSessionExpired() async {
    await _clearSessionStorage();
    _accessToken = null;
    _refreshToken = null;
    state = const AuthStateSignedOut();
  }

  // ----- High-level operations the screens call ----------------------------

  Future<void> signIn({
    required String email,
    required String password,
    bool rememberSession = true,
  }) async {
    // #9 — gate durable session persistence on "Keep me logged in"; carried
    // through to the OTP step (same controller) and honoured by _persistSession.
    _rememberSession = rememberSession;
    final result = await _repository.signIn(email: email, password: password);
    switch (result) {
      case SignInSession(:final session):
        await _persistSession(session);
        _setSignedIn(session);
        // The sign-in payload's user (AuthUser) carries only id/email/display
        // name — hydrate the authoritative app-role + registration status from
        // GET /app/users/me so the app does not treat the user as Guest.
        await reloadCurrentUser();
      case SignInOtpChallenge(:final otpToken):
        state = AuthStateAwaitingOtp(otpToken, email: email);
    }
  }

  Future<void> verifyOtp({required String code}) async {
    final current = state;
    if (current is! AuthStateAwaitingOtp) {
      return;
    }
    final session = await _repository.verifyOtp(
      otpToken: current.otpToken,
      code: code,
    );
    await _persistSession(session);
    _setSignedIn(session);
    // Hydrate the authoritative privilege (the token payload omits it) — L-4.
    await reloadCurrentUser();
  }

  /// Sign-up step 1 (Page 005). Creates a Visitor account (no privilege,
  /// under review, profile incomplete) and triggers the email-OTP — it does
  /// **not** sign the user in and does **not** change [AuthState]. The success
  /// is enumeration-resistant: identical for a new and an already-registered
  /// email (D-198). Throws [AuthFailure] on a wire error for the caller to map.
  Future<void> signUp({
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    await _repository.signUp(
      email: email,
      password: password,
      confirmPassword: confirmPassword,
    );
  }

  /// Sign-up step 2 (Page 006): verify the emailed 6-digit code. Moves the
  /// account Registered → EmailVerified; issues **no** session (anonymous flow).
  /// Throws [AuthFailure] on a wrong/expired/capped code for the caller to map.
  Future<void> verifyEmail({
    required String email,
    required String code,
  }) async {
    await _repository.verifyEmail(email: email, code: code);
  }

  /// Re-issue the sign-up verification code (Page 006). Returns the new code's
  /// lifetime in seconds for the resend cooldown. No session / [AuthState] change.
  Future<int> resendCode({required String email}) {
    return _repository.resendCode(email: email);
  }

  Future<void> signOut() async {
    final refreshToken = _refreshToken;
    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        await _repository.signOut(refreshToken: refreshToken);
      } on AuthFailure {
        // Best-effort: even if sign-out fails on the wire we still clear
        // the local session.
      }
    }
    await _clearSessionStorage();
    _accessToken = null;
    _refreshToken = null;
    state = const AuthStateSignedOut();
  }

  /// Called after sign-in or refresh to re-read the user from the server.
  Future<void> reloadCurrentUser() async {
    final current = state;
    if (current is! AuthStateSignedIn) {
      return;
    }
    try {
      final user = await _repository.getCurrentUser();
      final updated = current.session.copyWith(user: user);
      await _persistSession(updated);
      state = AuthStateSignedIn(updated);
    } on AuthFailure {
      // Leave the existing session in place; the next protected call will
      // surface the failure through the refresh interceptor.
    }
  }

  /// Re-reads the current user from `GET /app/users/me` and updates the session,
  /// **surfacing** any wire failure to the caller (unlike [reloadCurrentUser],
  /// which is best-effort and swallows it). Used by the registration-status
  /// screen (Page_011) so it can render Loading / Error distinctly and re-check
  /// on demand. Returns the refreshed user; throws [AuthFailure] on failure.
  Future<CurrentUser> refreshCurrentUser() async {
    final user = await _repository.getCurrentUser();
    final current = state;
    if (current is AuthStateSignedIn) {
      final updated = current.session.copyWith(user: user);
      await _persistSession(updated);
      state = AuthStateSignedIn(updated);
    }
    return user;
  }

  // ----- device key (biometric, D-172) -------------------------------------

  /// Enrols a device key for biometric sign-in (opt-in, after a password
  /// sign-in): generates a P-256 key pair, registers the public key with the
  /// server, and stores the private key + id in secure storage. No-op unless
  /// signed in (registration requires the bearer token).
  Future<void> enrolDeviceKey({
    String label = 'SIMF mobile',
    String? stepUpCode,
  }) async {
    if (state is! AuthStateSignedIn) {
      return;
    }
    final pair = _deviceKeyClient.generateKeyPair();
    final id = await _repository.registerDeviceKey(
      publicKeySpki: pair.publicKeySpkiBase64,
      label: label,
      stepUpCode: stepUpCode,
    );
    await _secureStorage.write(StorageKeys.deviceKeyId, id);
    await _secureStorage.write(
      StorageKeys.deviceKeyPrivate,
      pair.privateKeyBase64,
    );
  }

  /// #7a — request an emailed step-up code before enrolling biometric sign-in;
  /// returns the masked address the code was sent to (for the confirm screen).
  Future<String> sendBiometricStepUp() => _repository.sendBiometricStepUp();

  /// Whether a device key is enrolled on this device — the sign-in screen only
  /// offers the biometric button when this is true.
  Future<bool> hasEnrolledDeviceKey() async {
    final id = await _secureStorage.read(StorageKeys.deviceKeyId);
    return id != null && id.isNotEmpty;
  }

  /// Biometric re-open (Page_003 L-2): challenge → sign → sign-in-with-device-key
  /// → tokens. The caller gates this behind a successful on-device biometric
  /// prompt (local_auth). No-op if no device key is enrolled.
  Future<void> signInWithDeviceKey() async {
    final id = await _secureStorage.read(StorageKeys.deviceKeyId);
    final privateKey = await _secureStorage.read(StorageKeys.deviceKeyPrivate);
    if (id == null || id.isEmpty || privateKey == null || privateKey.isEmpty) {
      return;
    }
    final challenge = await _repository.issueDeviceKeyChallenge(id);
    final signature = _deviceKeyClient.sign(
      privateKeyBase64: privateKey,
      challengeBase64: challenge,
    );
    final session = await _repository.signInWithDeviceKey(
      deviceKeyId: id,
      challenge: challenge,
      signature: signature,
    );
    // #9 — a biometric re-open is an explicit "remember me on this device"
    // (the device key was enrolled for exactly that), so persist the session.
    _rememberSession = true;
    await _persistSession(session);
    _setSignedIn(session);
    await reloadCurrentUser();
  }

  /// Turns Face-ID sign-in off on this device: best-effort revokes the key on
  /// the server, then clears the local id + private key so the biometric path
  /// is disabled regardless of whether the server call succeeded.
  Future<void> disableDeviceKey() async {
    final id = await _secureStorage.read(StorageKeys.deviceKeyId);
    if (id != null && id.isNotEmpty) {
      try {
        await _repository.revokeDeviceKey(id);
      } catch (_) {
        // Best-effort: clearing the local key below disables the biometric
        // path even if the server revoke fails (the orphaned server key is
        // unusable without the private key we are about to delete).
      }
    }
    // Clear both local keys independently, so a failure on one delete does not
    // skip the other (either left behind would keep the biometric path alive).
    try {
      await _secureStorage.delete(StorageKeys.deviceKeyId);
    } catch (_) {
      // best-effort
    }
    try {
      await _secureStorage.delete(StorageKeys.deviceKeyPrivate);
    } catch (_) {
      // best-effort
    }
  }

  // ----- internals ---------------------------------------------------------

  void _setSignedIn(Session session) {
    _accessToken = session.accessToken;
    _refreshToken = session.refreshToken;
    state = AuthStateSignedIn(session);
  }

  Future<void> _restoreFromStorage() async {
    try {
      // Cold-start reads are time-boxed (D-295): a hung or slow platform
      // keystore must never stall the restore, because the router holds the
      // user on the splash while auth is still AuthStateInitial (router.dart),
      // so a stalled read would strand them on the splash forever. On timeout
      // the read throws and the catch-all below resolves to the signed-out
      // entry (Page_001 Logic L-6). The reads are independent, so they run
      // concurrently under a single cap well under the splash's 8s wait.
      final stored = await Future.wait(<Future<String?>>[
        _secureStorage.read(StorageKeys.accessToken),
        _secureStorage.read(StorageKeys.refreshToken),
        _secureStorage.read(StorageKeys.accessTokenExpiresAtIso),
        _secureStorage.read(StorageKeys.currentUserJson),
      ]).timeout(const Duration(seconds: 4));
      final access = stored[0];
      final refresh = stored[1];
      final expiresIso = stored[2];
      final userJson = stored[3];

      if (refresh == null || refresh.isEmpty) {
        state = const AuthStateSignedOut();
        return;
      }

      DateTime? expiresAt;
      if (expiresIso != null) {
        expiresAt = DateTime.tryParse(expiresIso);
      }
      CurrentUser? cachedUser;
      if (userJson != null) {
        try {
          final raw = jsonDecode(userJson);
          if (raw is Map<String, dynamic>) {
            cachedUser = _restoreCurrentUserFromMap(raw);
          }
        } catch (_) {
          cachedUser = null;
        }
      }

      // The signed-in session we can rebuild from cache alone — used by both
      // the valid-token fast path and the offline-degraded resume (L-4/L-6).
      final cachedSession =
          (cachedUser != null && access != null && access.isNotEmpty)
              ? Session(
                  accessToken: access,
                  refreshToken: refresh,
                  accessTokenExpiresAt: expiresAt ?? DateTime.now(),
                  user: cachedUser,
                )
              : null;

      // Fast path: a still-valid access token → restore immediately, then
      // re-read the authoritative privilege from GET /app/users/me before
      // route-out (Page_001 Logic L-4). The cached privilege may be stale or
      // defaulted; a network error is swallowed by reloadCurrentUser, keeping
      // the cached session (L-6).
      if (cachedSession != null &&
          expiresAt != null &&
          expiresAt.isAfter(DateTime.now())) {
        _setSignedIn(cachedSession);
        await reloadCurrentUser();
        return;
      }

      // The access token is missing or expired — silently refresh (Page_001
      // Logic L-4). This path uses the repository directly (not the
      // AuthTokenSource refresh used by the 401 interceptor) so it can tell an
      // offline failure from an expired-session failure.
      _refreshToken = refresh;
      try {
        final session = await _repository.refresh(refreshToken: refresh);
        await _persistSession(session);
        _setSignedIn(session);
        // The token payload's user (AuthUser) carries only id/email/displayName,
        // so the privilege would default to Guest — hydrate the real app-role +
        // registration status from GET /app/users/me before route-out (L-4).
        await reloadCurrentUser();
      } on NetworkUnavailable {
        // Offline / server unreachable (Logic L-6): resume on the cached
        // identity in a degraded state rather than stranding the user. With no
        // cached identity there is nothing to resume to — sign out.
        if (cachedSession != null) {
          _setSignedIn(cachedSession);
        } else {
          _refreshToken = null;
          state = const AuthStateSignedOut();
        }
      } on AuthFailure {
        // Expired / revoked / invalid refresh token (Logic L-4): clear and sign
        // out; do not loop.
        await _clearSessionStorage();
        _accessToken = null;
        _refreshToken = null;
        state = const AuthStateSignedOut();
      }
    } catch (_) {
      // Any unexpected failure (e.g. secure-storage throwing on a corrupt
      // keystore) must never strand the app on the splash (Logic L-6): fall
      // back to the signed-out entry.
      _accessToken = null;
      _refreshToken = null;
      state = const AuthStateSignedOut();
    }
  }

  Future<void> _persistSession(Session session) async {
    if (!_rememberSession) {
      // #9 — "Keep me logged in" is off: keep the session in memory only and
      // make sure nothing is left in durable storage to restore next launch.
      await _clearSessionStorage();
      return;
    }
    await Future.wait(<Future<void>>[
      _secureStorage.write(StorageKeys.accessToken, session.accessToken),
      _secureStorage.write(StorageKeys.refreshToken, session.refreshToken),
      _secureStorage.write(
        StorageKeys.accessTokenExpiresAtIso,
        session.accessTokenExpiresAt.toIso8601String(),
      ),
      _secureStorage.write(
        StorageKeys.currentUserJson,
        jsonEncode(_currentUserToMap(session.user)),
      ),
    ]);
  }

  Future<void> _clearSessionStorage() => _secureStorage.clearAuthValues();
}

/// The [AuthController] provider. Read the notifier for the
/// [AuthTokenSource] contract; the notifier itself is the implementation.
final authControllerProvider =
    NotifierProvider<AuthController, AuthState>(AuthController.new);

Map<String, dynamic> _currentUserToMap(CurrentUser u) {
  return <String, dynamic>{
    'id': u.id,
    'email': u.email,
    'displayName': u.displayName,
    'appRole': u.appRole.wireName,
    'preferredLanguage': u.preferredLanguage.code,
    'registrationStatus': u.registrationStatus.wireName,
    'avatarUrl': u.avatarUrl,
    'profileComplete': u.profileComplete,
  };
}

CurrentUser _restoreCurrentUserFromMap(Map<String, dynamic> json) {
  return CurrentUser(
    id: json['id'] as String? ?? '',
    email: json['email'] as String? ?? '',
    displayName: json['displayName'] as String? ?? '',
    appRole: AppRole.fromJson(json['appRole']),
    preferredLanguage: PreferredLanguage.fromJson(json['preferredLanguage']),
    registrationStatus: RegistrationStatus.fromJson(json['registrationStatus']),
    avatarUrl: json['avatarUrl'] as String?,
    profileComplete: json['profileComplete'] as bool? ?? false,
  );
}
