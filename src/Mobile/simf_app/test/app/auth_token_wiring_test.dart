import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-372 regression — the Wave-1 live E2E found the post-sign-in
/// `GET /app/users/me` going out WITHOUT the Authorization header: the old
/// override (`ref.read(authControllerProvider.notifier)` resolved eagerly)
/// formed a circular dependency (controller → repository → api client →
/// token source → controller) and the interceptors ended up holding a
/// never-initialised controller. This test mirrors `main.dart`'s wiring
/// (now [DeferredAuthTokenSource]) against a header-recording dio adapter
/// replaying the real wire payloads captured from the running backend —
/// with the OLD wiring it fails (CircularDependencyError / missing
/// header); with the deferred source it passes.
class _RecordedRequest {
  _RecordedRequest(this.path, this.headers);
  final String path;
  final Map<String, dynamic> headers;
}

class _RecordingAdapter implements HttpClientAdapter {
  final List<_RecordedRequest> requests = <_RecordedRequest>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(
      _RecordedRequest(options.path, Map<String, dynamic>.of(options.headers)),
    );
    final path = options.path;
    String body;
    if (path.contains('/auth/sign-in')) {
      body = jsonEncode(<String, dynamic>{
        'success': true,
        'data': <String, dynamic>{
          'mfaRequired': false,
          'mfaToken': null,
          'otpToken': null,
          'tokens': <String, dynamic>{
            'accessToken': 'TEST-ACCESS-TOKEN',
            'refreshToken': 'TEST-REFRESH-TOKEN',
            'tokenType': 'Bearer',
            'accessTokenExpiresInSeconds': 1800,
            'user': <String, dynamic>{
              'id': 'u1',
              'email': 'e2e@example.sa',
              'displayName': 'e2e@example.sa',
            },
          },
          'accountState': <String, dynamic>{'state': 'EmailVerified'},
          'passwordChangeToken': null,
        },
        'error': null,
        'meta': null,
      });
    } else if (path.contains('/users/me')) {
      body = jsonEncode(<String, dynamic>{
        'success': true,
        'data': <String, dynamic>{
          'id': 'u1',
          'email': 'e2e@example.sa',
          'displayName': 'e2e@example.sa',
          'appRole': 'Visitor',
          'preferredLanguage': 'ar',
          'registrationStatus': 'Pending',
        },
        'error': null,
        'meta': null,
      });
    } else {
      body = jsonEncode(<String, dynamic>{
        'success': true,
        'data': null,
        'error': null,
        'meta': null,
      });
    }
    return ResponseBody.fromString(
      body,
      200,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>[Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};
  @override
  String? getString(String key) =>
      _store[key] is String ? _store[key]! as String : null;
  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => null;
  @override
  Future<bool> setBool(String key, bool value) async => true;
  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

class _InMemorySecureStorage implements SimfSecureStorage {
  final Map<String, String> _store = <String, String>{};
  @override
  Future<String?> read(String key) async => _store[key];
  @override
  Future<void> write(String key, String value) async => _store[key] = value;
  @override
  Future<void> delete(String key) async => _store.remove(key);
  @override
  Future<void> clearAuthValues() async => _store.clear();
}

void main() {
  test(
      'after signIn, the follow-up /users/me request carries the '
      'Authorization header (main.dart wiring)', () async {
    final adapter = _RecordingAdapter();
    const config = SimfDataConfig(
      baseUrl: 'http://test.local/api/v1',
      appKey: 'test-key',
      deviceType: SimfDeviceType.android,
    );

    final container = ProviderContainer(
      overrides: <Override>[
        // The exact main.dart override pattern.
        simfDataConfigProvider.overrideWithValue(config),
        simfPrefsStorageProvider.overrideWithValue(_FakePrefs()),
        simfSecureStorageProvider.overrideWithValue(_InMemorySecureStorage()),
        currentLanguageCodeProvider.overrideWith((ref) => () => 'ar'),
        authTokenSourceProvider.overrideWith((ref) => AuthTokenBridge()),
        // The real client (real interceptors), over the recording adapter.
        simfApiClientProvider.overrideWith((ref) {
          final dio = Dio(
            BaseOptions(
              baseUrl: config.baseUrl,
              responseType: ResponseType.json,
              validateStatus: (_) => true,
            ),
          )..httpClientAdapter = adapter;
          return SimfApiClient.build(
            config: ref.watch(simfDataConfigProvider),
            tokenSource: ref.watch(authTokenSourceProvider),
            currentLanguageCode: ref.watch(currentLanguageCodeProvider),
            dioOverride: dio,
          );
        }),
      ],
    );
    addTearDown(container.dispose);

    // The app's real lifecycle: the splash WATCHES the state provider first
    // (initialising the controller chain), then screens call the notifier.
    container.read(authControllerProvider);
    final controller = container.read(authControllerProvider.notifier);
    await controller.signIn(email: 'e2e@example.sa', password: 'Password1');

    final me = adapter.requests
        .where((r) => r.path.contains('/users/me'))
        .toList();
    expect(
      me,
      isNotEmpty,
      reason: 'sign-in must hydrate via GET /users/me',
    );
    final auth = me.first.headers.entries
        .where((e) => e.key.toLowerCase() == 'authorization')
        .map((e) => e.value)
        .toList();
    expect(
      auth,
      equals(<Object>['Bearer TEST-ACCESS-TOKEN']),
      reason: 'the hydration call must carry the just-issued bearer token',
    );
  });
}
