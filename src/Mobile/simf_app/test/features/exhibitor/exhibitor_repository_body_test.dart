import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-426: lead capture posts the entry-badge `qrId`, never the contacts share
/// `token`. The server 404s a swap, which the scanner shows as "no matching
/// badge" — indistinguishable from a genuinely unknown visitor.

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
        // `scanByBadge` decodes with VisitorCard.fromData, which casts to Map.
        data: <String, dynamic>{
          'success': true,
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

ExhibitorRepository _repository(_CapturingInterceptor capture) {
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
  return ExhibitorRepository(client);
}

void main() {
  group('ExhibitorRepository.scanByBadge posts the badge qrId', () {
    test('sends { qrId } and trims the scanned value', () async {
      final capture = _CapturingInterceptor();

      await _repository(capture).scanByBadge('  BADGE-999  ');

      // Exact-map, so a rename to `token` reds AND a stray extra key reds.
      expect(capture.body, <String, dynamic>{'qrId': 'BADGE-999'});
      expect(capture.path, '/app/exhibitor/visitors/scan');
    });

    test('an optional note rides alongside, never in place of, the qrId',
        () async {
      final capture = _CapturingInterceptor();

      await _repository(capture).scanByBadge('BADGE-999', note: ' lead A ');

      expect(capture.body, <String, dynamic>{
        'qrId': 'BADGE-999',
        'note': 'lead A',
      });
    });
  });
}
