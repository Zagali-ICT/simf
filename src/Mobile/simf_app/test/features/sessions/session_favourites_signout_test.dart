import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Pins: `sessionFavouritesProvider` watches `authControllerProvider`, so one
/// account's favourites cannot survive sign-out into the next account.
class _ExplodingAdapter implements HttpClientAdapter {
  int calls = 0;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    calls++;
    return ResponseBody.fromString(
      '{"success":true,"data":["s1","s2"],"error":null,"meta":null}',
      200,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _FakeAuth extends AuthController {
  _FakeAuth(this._initial);
  final AuthState _initial;

  @override
  AuthState build() => _initial;

  /// Not `signOut`: the real one reaches `late final _repository`, which only
  /// the real `build()` initialises.
  void becomeSignedOut() => state = const AuthStateSignedOut();
}

AuthState _signedIn() => AuthStateSignedIn(
      Session(
        accessToken: 'A',
        refreshToken: 'R',
        accessTokenExpiresAt: DateTime(2099),
        user: CurrentUser(
          id: 'u1',
          email: 'a@simf.test',
          displayName: 'Alice',
          appRole: AppRole.visitor,
          preferredLanguage: PreferredLanguage.fromJson('en'),
          registrationStatus: RegistrationStatus.approved,
        ),
      ),
    );

void main() {
  late _ExplodingAdapter adapter;
  late SimfApiClient client;

  setUp(() {
    adapter = _ExplodingAdapter();
    final dio = Dio()..httpClientAdapter = adapter;
    client = SimfApiClient.build(
      config: const SimfDataConfig(
        baseUrl: 'http://test.local/api/v1',
        appKey: 'test-key',
        deviceType: SimfDeviceType.android,
      ),
      tokenSource: const NoAuthTokenSource(),
      currentLanguageCode: () => 'ar',
      dioOverride: dio,
    );
  });

  ProviderContainer containerFor(_FakeAuth auth) {
    final container = ProviderContainer(
      retry: (count, error) => null,
      overrides: <Override>[
        simfApiClientProvider.overrideWithValue(client),
        authControllerProvider.overrideWith(() => auth),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  test('a signed-out session has no favourites and asks the API for none',
      () async {
    final container = containerFor(_FakeAuth(const AuthStateSignedOut()));

    final ids = await container.read(sessionFavouritesProvider.future);

    expect(
      ids,
      isEmpty,
      reason: 'A signed-out session must start with no favourites. A non-empty '
          'set here means the provider fetched with no account behind it, so '
          "whatever the server answered became this session's hearts.",
    );
    expect(
      adapter.calls,
      isZero,
      reason: 'A signed-out user has no favourites to fetch. Calling the '
          'endpoint anyway means the guard is gone and whatever the server '
          'returns becomes this session.',
    );
  });

  test("signing out drops the previous account's favourites", () async {
    final auth = _FakeAuth(_signedIn());
    final container = containerFor(auth);

    // Keeps the notifier alive even if the provider stops watching auth, so
    // the failure is the expect below rather than an uninitialized-notifier
    // throw from `becomeSignedOut()`.
    final authSub = container.listen<AuthState>(
      authControllerProvider,
      (_, __) {},
    );
    addTearDown(authSub.close);

    expect(await container.read(sessionFavouritesProvider.future), <String>{
      's1',
      's2',
    });

    auth.becomeSignedOut();

    expect(
      await container.read(sessionFavouritesProvider.future),
      isEmpty,
      reason: 'The set outlived the account. The next user to sign in on this '
          'device would see these hearts filled, and clearing one would DELETE '
          'a favourite belonging to the previous account.',
    );
  });
}
