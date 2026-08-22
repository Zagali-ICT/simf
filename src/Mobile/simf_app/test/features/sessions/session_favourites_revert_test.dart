import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Pins: `toggle` flips optimistically but REVERTS on failure, in both
/// directions — without it a heart the server refused stays flipped until the
/// app restarts, and `toggle` rethrows either way so nothing else notices.

/// Answers the favourites LOAD, then fails whatever write comes next.
class _WriteFailsAdapter implements HttpClientAdapter {
  final List<String> writes = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.method == 'GET') {
      return ResponseBody.fromString(
        '{"success":true,"data":["s1","s2"],"error":null,"meta":null}',
        200,
        headers: <String, List<String>>{
          Headers.contentTypeHeader: <String>['application/json'],
        },
      );
    }
    writes.add('${options.method} ${options.path}');
    return ResponseBody.fromString(
      '{"success":false,"data":null,'
      '"error":{"code":"SERVER_ERROR","message":"boom"},"meta":null}',
      500,
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
  late _WriteFailsAdapter adapter;
  late SimfApiClient client;

  setUp(() {
    adapter = _WriteFailsAdapter();
    final dio = Dio()..httpClientAdapter = adapter;
    client = SimfApiClient.build(
      config: const SimfDataConfig(
        baseUrl: 'http://test.local/api/v1',
        appKey: 'test-key',
        deviceType: SimfDeviceType.android,
      ),
      tokenSource: const NoAuthTokenSource(),
      currentLanguageCode: () => 'en',
      dioOverride: dio,
    );
  });

  ProviderContainer buildContainer() {
    final container = ProviderContainer(
      retry: (count, error) => null,
      overrides: <Override>[
        simfApiClientProvider.overrideWithValue(client),
        authControllerProvider.overrideWith(() => _FakeAuth(_signedIn())),
      ],
    );
    addTearDown(container.dispose);
    return container;
  }

  group('a favourite the server refused does not stay flipped', () {
    test('a failed ADD leaves the heart empty', () async {
      final container = buildContainer();
      final controller = container.read(sessionFavouritesProvider.notifier);
      expect(await container.read(sessionFavouritesProvider.future), <String>{
        's1',
        's2',
      });

      await expectLater(
        controller.toggle('s3'),
        throwsA(isA<ApiFailure>()),
      );

      expect(adapter.writes.single, contains('POST'));
      expect(
        container.read(sessionFavouritesProvider).value,
        <String>{'s1', 's2'},
        reason: 'The add failed, so s3 is not a favourite. Leaving it in the '
            'set shows a filled heart the server never stored, counts it on '
            'the My Area tile, and makes the next tap send a DELETE for a row '
            'that does not exist.',
      );
      expect(controller.isFavourite('s3'), isFalse);
    });

    test('a failed REMOVE leaves the heart filled', () async {
      final container = buildContainer();
      final controller = container.read(sessionFavouritesProvider.notifier);
      await container.read(sessionFavouritesProvider.future);

      await expectLater(
        controller.toggle('s1'),
        throwsA(isA<ApiFailure>()),
      );

      expect(adapter.writes.single, contains('DELETE'));
      expect(
        container.read(sessionFavouritesProvider).value,
        <String>{'s1', 's2'},
        reason: 'The delete failed, so s1 is still a favourite on the server. '
            'An emptied heart here hides a row the user still has, and their '
            'next tap re-POSTs a favourite that was never removed.',
      );
      expect(controller.isFavourite('s1'), isTrue);
    });

    // The control: a "fix" that waited for the server would pass both tests
    // above while making every heart feel laggy.
    test('the flip is applied optimistically, before the call is made',
        () async {
      final container = buildContainer();
      final controller = container.read(sessionFavouritesProvider.notifier);
      await container.read(sessionFavouritesProvider.future);

      final pending = controller.toggle('s3');
      // Not awaited yet — the set must ALREADY carry s3.
      expect(controller.isFavourite('s3'), isTrue);

      await expectLater(pending, throwsA(isA<ApiFailure>()));
    });
  });
}
