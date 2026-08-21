import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The contacts wire carries TWO different identifiers and they are easy to
/// confuse: a `token` (the rotatable share token a visitor hands out) and a
/// `qrId` (the opaque entry-badge id the gate scanner reads). They are NOT
/// interchangeable, and the server answers a swapped one with a plain 404 —
/// so a swap here is silent at every layer above it and looks to the user
/// like "that contact does not exist".
///
/// These tests drive the repository and the decoder and assert the actual key
/// on the wire, because nothing else in the suite does: the screen tests fake
/// the repository, so they keep passing with either key.

/// Captures the request body of the one POST and short-circuits with a canned
/// success envelope — no real backend, and the body is the exact Map the
/// repository passed (interceptors run before serialization).
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
          // A VisitorCard-shaped payload: `resolve` decodes with
          // VisitorCard.fromData, which casts `data` to a Map.
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
    // Both keys are present on purpose: reading the wrong one then returns a
    // real (wrong) value instead of falling through to the blank default, so
    // this discriminates a KEY SWAP and not merely a missing field.
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
