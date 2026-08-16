import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Every endpoint (the protected call AND `/app/auth/refresh`) returns 401 —
/// the stale-session case that used to deadlock the refresh.
class _AllUnauthorizedAdapter implements HttpClientAdapter {
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    return ResponseBody.fromString(
      '{"success":false,"data":null,'
      '"error":{"code":"unauthorized","messageEn":"Unauthorized",'
      '"messageAr":"غير مصرح"},"meta":null}',
      401,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

/// Faithfully models the real `AuthController`: a single-flight refresh that
/// itself calls the client's refresh endpoint. Before the fix, a 401 on that
/// refresh call re-entered this same in-flight future → the refresh awaited
/// itself → permanent deadlock.
class _SingleFlightBridge implements AuthTokenSource {
  SimfApiClient? client;
  int refreshCalls = 0;
  Future<bool>? _inFlight;

  @override
  String? currentAccessToken() => 'expired-access-token';

  @override
  Future<bool> refresh() =>
      _inFlight ??= _once().whenComplete(() => _inFlight = null);

  Future<bool> _once() async {
    refreshCalls++;
    try {
      await client!.post<bool>(
        '/app/auth/refresh',
        body: <String, dynamic>{'refreshToken': 'r'},
        decodeData: (_) => true,
        skipAuthRefresh: true,
      );
      return true;
    } on ApiFailure {
      return false;
    }
  }

  @override
  Future<void> onSessionExpired() async {}
}

void main() {
  test(
    'a 401 on the refresh call resolves cleanly without deadlocking '
    '(refresh re-entry guard)',
    () async {
      final bridge = _SingleFlightBridge();
      final dio = Dio(
        BaseOptions(
          baseUrl: 'https://api.test/api/v1',
          validateStatus: (_) => true,
        ),
      )..httpClientAdapter = _AllUnauthorizedAdapter();
      final client = SimfApiClient.build(
        config: const SimfDataConfig(
          baseUrl: 'https://api.test/api/v1',
          appKey: 'k',
          deviceType: SimfDeviceType.android,
        ),
        tokenSource: bridge,
        currentLanguageCode: () => 'ar',
        dioOverride: dio,
      );
      bridge.client = client;

      // Before the fix this future never settles (the single-flight refresh
      // awaits itself) and the test hits its timeout. With the fix the refresh
      // call's own 401 is NOT re-refreshed, so the original request fails fast
      // with the 401 ApiFailure.
      await expectLater(
        client.get<bool>('/protected', decodeData: (_) => true),
        throwsA(isA<ApiFailure>()),
      );

      // The single-flight refresh ran exactly once; the refresh's own 401 did
      // not spawn a second refresh.
      expect(bridge.refreshCalls, 1);
    },
    timeout: const Timeout(Duration(seconds: 15)),
  );
}
