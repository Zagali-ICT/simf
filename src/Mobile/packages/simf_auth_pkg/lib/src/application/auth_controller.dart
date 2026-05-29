import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../data/auth_api.dart';
import '../data/auth_repository_impl.dart';
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

/// A sign-in needs TOTP. The router redirects to the TOTP screen.
class AuthStateAwaitingTotp extends AuthState {
  const AuthStateAwaitingTotp(this.mfaToken);
  final String mfaToken;
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

  @override
  AuthState build() {
    _repository = ref.watch(authRepositoryProvider);
    _secureStorage = ref.watch(simfSecureStorageProvider);
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
  Future<bool> refresh() async {
    final refreshToken = _refreshToken;
    if (refreshToken == null || refreshToken.isEmpty) {
      return false;
    }
    try {
      final session = await _repository.refresh(refreshToken: refreshToken);
      await _persistSession(session);
      _setSignedIn(session);
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
  }) async {
    final result = await _repository.signIn(email: email, password: password);
    switch (result) {
      case SignInSession(:final session):
        await _persistSession(session);
        _setSignedIn(session);
      case SignInChallenge(:final mfaToken):
        state = AuthStateAwaitingTotp(mfaToken);
    }
  }

  Future<void> verifyTotp({required String code}) async {
    final current = state;
    if (current is! AuthStateAwaitingTotp) {
      return;
    }
    final session = await _repository.verifyTotp(
      mfaToken: current.mfaToken,
      code: code,
    );
    await _persistSession(session);
    _setSignedIn(session);
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

  // ----- internals ---------------------------------------------------------

  void _setSignedIn(Session session) {
    _accessToken = session.accessToken;
    _refreshToken = session.refreshToken;
    state = AuthStateSignedIn(session);
  }

  Future<void> _restoreFromStorage() async {
    final access = await _secureStorage.read(StorageKeys.accessToken);
    final refresh = await _secureStorage.read(StorageKeys.refreshToken);
    final expiresIso = await _secureStorage.read(
      StorageKeys.accessTokenExpiresAtIso,
    );
    final userJson = await _secureStorage.read(StorageKeys.currentUserJson);

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

    if (access != null &&
        access.isNotEmpty &&
        expiresAt != null &&
        cachedUser != null &&
        expiresAt.isAfter(DateTime.now())) {
      final session = Session(
        accessToken: access,
        refreshToken: refresh,
        accessTokenExpiresAt: expiresAt,
        user: cachedUser,
      );
      _setSignedIn(session);
      return;
    }

    // Refresh exists but the access token is missing or expired — try a
    // refresh now. If it fails the user signs in again.
    _refreshToken = refresh;
    final refreshed = await this.refresh();
    if (!refreshed) {
      _refreshToken = null;
      state = const AuthStateSignedOut();
    }
  }

  Future<void> _persistSession(Session session) async {
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
  );
}
