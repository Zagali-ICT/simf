import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:simf_data_pkg/src/api/interceptors/headers_interceptor.dart';

class _FakeTokenSource implements AuthTokenSource {
  _FakeTokenSource(this._token);
  String? _token;
  @override
  String? currentAccessToken() => _token;
  @override
  Future<bool> refresh() async => false;
  @override
  Future<void> onSessionExpired() async {}
}

void main() {
  group('HeadersInterceptor', () {
    late RequestOptions options;
    late _FakeTokenSource tokens;
    late String currentLanguage;

    setUp(() {
      options = RequestOptions(path: '/auth/sign-in', method: 'POST');
      tokens = _FakeTokenSource(null);
      currentLanguage = 'ar';
    });

    HeadersInterceptor build() => HeadersInterceptor(
          config: const SimfDataConfig(
            baseUrl: 'https://api.test/api/v1',
            appKey: 'test-app-key',
            deviceType: SimfDeviceType.android,
          ),
          tokenSource: tokens,
          currentLanguageCode: () => currentLanguage,
        );

    test('attaches the four required standard headers', () {
      final handler = _CapturingRequestHandler();
      build().onRequest(options, handler);

      expect(handler.captured!.headers['X-App-Key'], equals('test-app-key'));
      expect(handler.captured!.headers['X-Device-Type'], equals('Android'));
      expect(handler.captured!.headers['Accept-Language'], equals('ar'));
      expect(handler.captured!.headers.containsKey('Authorization'), isFalse);
    });

    test('attaches Authorization when a token is present', () {
      tokens = _FakeTokenSource('eyJhbGc.token.value');
      final handler = _CapturingRequestHandler();
      build().onRequest(options, handler);

      expect(
        handler.captured!.headers['Authorization'],
        equals('Bearer eyJhbGc.token.value'),
      );
    });

    test('omits Authorization when the token is empty string', () {
      tokens = _FakeTokenSource('');
      final handler = _CapturingRequestHandler();
      build().onRequest(options, handler);

      expect(handler.captured!.headers.containsKey('Authorization'), isFalse);
    });

    test('reads the language on every request (picks up changes)', () {
      currentLanguage = 'en';
      final handler1 = _CapturingRequestHandler();
      build().onRequest(options, handler1);
      expect(handler1.captured!.headers['Accept-Language'], equals('en'));

      currentLanguage = 'ar';
      final handler2 = _CapturingRequestHandler();
      build().onRequest(options, handler2);
      expect(handler2.captured!.headers['Accept-Language'], equals('ar'));
    });

    test('iOS device type renders as "iOS"', () {
      final interceptor = HeadersInterceptor(
        config: const SimfDataConfig(
          baseUrl: 'https://api.test/api/v1',
          appKey: 'k',
          deviceType: SimfDeviceType.ios,
        ),
        tokenSource: tokens,
        currentLanguageCode: () => 'ar',
      );
      final handler = _CapturingRequestHandler();
      interceptor.onRequest(options, handler);

      expect(handler.captured!.headers['X-Device-Type'], equals('iOS'));
    });
  });
}

class _CapturingRequestHandler extends RequestInterceptorHandler {
  RequestOptions? captured;

  @override
  void next(RequestOptions requestOptions) {
    captured = requestOptions;
  }
}
