import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/questions/data/questions_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
          'data': true,
          'error': null,
          'meta': null,
        },
      ),
    );
  }
}

QuestionsRepository _repository(_CapturingInterceptor capture) {
  final dio = Dio(
    BaseOptions(baseUrl: 'https://api.test/api/v1', validateStatus: (_) => true),
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
  return QuestionsRepository(client);
}

void main() {
  group('QuestionsRepository.submitQuestion', () {
    test('S-5 — posts isAtVenue == false (the app never self-certifies presence)',
        () async {
      final capture = _CapturingInterceptor();
      final repo = _repository(capture);

      await repo.submitQuestion(
        's1',
        questionText: 'How deep is the reef?',
        recipientIndex: 0,
      );

      final body = capture.body!;
      // The core S-5 assertion: the self-assert flag is no longer trusted, so the
      // app sends it false and lets the server be the authoritative venue gate.
      expect(body['isAtVenue'], isFalse);
      expect(body['questionText'], 'How deep is the reef?');
      expect(body['recipient'], 0);
      expect(capture.path, '/app/sessions/s1/questions');
    });
  });
}
