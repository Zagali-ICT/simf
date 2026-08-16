import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Captures the request path and returns a canned envelope, so the two
/// self-service uploads (avatar / ID document, D-682) can be asserted to hit the
/// right endpoint and to surface a wire error as [ApiFailure] — without a real
/// backend. The MIME derivation itself is covered by
/// `profile_repository_mime_test.dart`.
class _CapturingAdapter implements HttpClientAdapter {
  _CapturingAdapter({this.succeed = true});

  final List<String> paths = <String>[];
  final bool succeed;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    paths.add(options.path);
    final body = succeed
        ? '{"success":true,"data":true,"error":null,"meta":null}'
        : '{"success":false,"data":null,'
            '"error":{"code":"bad_request","messageEn":"Bad",'
            '"messageAr":"خطأ"},"meta":null}';
    return ResponseBody.fromString(
      body,
      succeed ? 200 : 400,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _StaticToken implements AuthTokenSource {
  @override
  String? currentAccessToken() => 'access-token';

  @override
  Future<bool> refresh() async => false;

  @override
  Future<void> onSessionExpired() async {}
}

ProfileRepository _repository(_CapturingAdapter adapter) {
  final dio = Dio(
    BaseOptions(
        baseUrl: 'https://api.test/api/v1', validateStatus: (_) => true,),
  )..httpClientAdapter = adapter;
  final client = SimfApiClient.build(
    config: const SimfDataConfig(
      baseUrl: 'https://api.test/api/v1',
      appKey: 'k',
      deviceType: SimfDeviceType.android,
    ),
    tokenSource: _StaticToken(),
    currentLanguageCode: () => 'ar',
    dioOverride: dio,
  );
  return ProfileRepository(client);
}

void main() {
  final bytes = Uint8List.fromList(<int>[1, 2, 3, 4]);

  group('ProfileRepository uploads', () {
    test('uploadAvatar posts the face photo to /app/account/avatar', () async {
      final adapter = _CapturingAdapter();
      final ok = await _repository(adapter)
          .uploadAvatar(bytes: bytes, filename: 'face.jpg');
      expect(ok, isTrue);
      expect(adapter.paths.single, '/app/account/avatar');
    });

    test('uploadIdImage posts the document to the id-image endpoint', () async {
      final adapter = _CapturingAdapter();
      final ok = await _repository(adapter)
          .uploadIdImage(bytes: bytes, filename: 'id.png');
      expect(ok, isTrue);
      expect(adapter.paths.single, '/app/account/user-profile/id-image');
    });

    test('a non-succeeded envelope surfaces as ApiFailure', () async {
      final adapter = _CapturingAdapter(succeed: false);
      await expectLater(
        _repository(adapter).uploadAvatar(bytes: bytes, filename: 'face.jpg'),
        throwsA(isA<ApiFailure>()),
      );
    });
  });
}
