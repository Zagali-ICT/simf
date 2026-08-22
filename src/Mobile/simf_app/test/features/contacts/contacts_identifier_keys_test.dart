import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Pins the contacts wire key as `token` (the rotatable share token), never
/// `qrId` (the entry-badge id): the server answers a swap with a plain 404,
/// which the user reads as "that contact does not exist".

/// Captures the POST body — still the raw Map, since interceptors run before
/// serialization — and short-circuits with a canned success envelope.
class _CapturingInterceptor extends Interceptor {
  Map<String, dynamic>? body;
  String? path;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    path = options.path;
    body = options.data as Map<String, dynamic>?;
    handler.resolve(
      Response<dynamic>(
        requestOptions: options,
        statusCode: 200,
        data: <String, dynamic>{
          'success': true,
          // VisitorCard-shaped: `resolve` decodes via VisitorCard.fromData.
          'data': <String, dynamic>{
            'userId': 'u1',
            'name': 'Raed Al-Salem',
            'nameArabic': 'رائد السالم',
            'available': true,
          },
          'error': null,
          'meta': null,
        },
      ),
    );
  }
}

ContactsRepository _repository(_CapturingInterceptor capture) {
  final dio = Dio(
    BaseOptions(
      baseUrl: 'https://api.test/api/v1',
      validateStatus: (_) => true,
    ),
  )..interceptors.add(capture);
  final client = SimfApiClient.build(
    config: const SimfDataConfig(
      baseUrl: 'https://api.test/api/v1',
      appKey: 'k',
      deviceType: SimfDeviceType.android,
    ),
    tokenSource: const NoAuthTokenSource(),
    currentLanguageCode: () => 'en',
    dioOverride: dio,
  );
  return ContactsRepository(client);
}

void main() {
  group('ContactsRepository posts the SHARE TOKEN, never a qrId', () {
    test('resolve sends { token } and nothing else', () async {
      final capture = _CapturingInterceptor();

      await _repository(capture).resolve('TOK-123');

      // Exact-map, so the swap to `qrId` reds AND a stray extra key reds.
      expect(capture.body, <String, dynamic>{'token': 'TOK-123'});
      expect(capture.path, '/app/contacts/resolve');
    });

    test('save sends { token, note } — the note never displaces the key',
        () async {
      final capture = _CapturingInterceptor();

      await _repository(capture).save('TOK-123', '  met at booth 4  ');

      expect(capture.body, <String, dynamic>{
        'token': 'TOK-123',
        'note': 'met at booth 4',
      });
      expect(capture.path, '/app/contacts/save');
    });
  });

  group('VisitorShareToken decodes the `token` key', () {
    // Both keys present on purpose: a swap then reads a real wrong value
    // rather than the blank default, so this catches the swap itself.
    test('reads token, not a co-present qrId', () {
      final decoded = VisitorShareToken.fromData(const <String, dynamic>{
        'token': 'TOK-123',
        'qrId': 'BADGE-999',
      });

      expect(decoded.token, 'TOK-123');
    });

    test('a payload with no token falls back to blank, never to another id',
        () {
      final decoded = VisitorShareToken.fromData(const <String, dynamic>{
        'qrId': 'BADGE-999',
      });

      expect(decoded.token, '');
    });
  });
}
