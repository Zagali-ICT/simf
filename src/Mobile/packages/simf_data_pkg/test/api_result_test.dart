import 'package:flutter_test/flutter_test.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

void main() {
  group('ApiResult.fromJson', () {
    test('decodes a success envelope and calls the data decoder', () {
      final json = <String, dynamic>{
        'success': true,
        'data': {'email': 'r.alsalem@example.sa', 'name': 'Raed'},
        'error': null,
        'meta': null,
      };

      final result = ApiResult<Map<String, String>>.fromJson(
        json,
        (data) => (data! as Map<String, dynamic>)
            .map<String, String>((k, v) => MapEntry(k, v as String)),
      );

      expect(result.success, isTrue);
      expect(result.hasData, isTrue);
      expect(result.data!['email'], equals('r.alsalem@example.sa'));
      expect(result.error, isNull);
    });

    test('decodes a failure envelope and does NOT call the data decoder', () {
      final json = <String, dynamic>{
        'success': false,
        'data': null,
        'error': {
          'code': 'AUTH_INVALID_CREDENTIALS',
          'message': 'The email or password is incorrect.',
          'details': <Map<String, dynamic>>[],
        },
        'meta': null,
      };

      var decoderCalled = false;
      final result = ApiResult<Object>.fromJson(json, (data) {
        decoderCalled = true;
        return Object();
      });

      expect(decoderCalled, isFalse);
      expect(result.success, isFalse);
      expect(result.hasData, isFalse);
      expect(result.error!.code, equals('AUTH_INVALID_CREDENTIALS'));
      expect(result.error!.message, contains('incorrect'));
      expect(result.error!.details, isEmpty);
    });

    test('decodes the Arabic message alongside the English one (#8/#11)', () {
      final json = <String, dynamic>{
        'success': false,
        'data': null,
        'error': {
          'code': 'AUTH_INVALID_CREDENTIALS',
          'message': 'The email or password is incorrect.',
          'messageArabic': 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
          'details': <Map<String, dynamic>>[],
        },
        'meta': null,
      };
      final result = ApiResult<Object>.fromJson(json, (_) => Object());
      expect(result.error!.message, contains('incorrect'));
      expect(result.error!.messageArabic, contains('غير صحيحة'));
      expect(result.error!.localized(true), contains('غير صحيحة'));
      expect(result.error!.localized(false), contains('incorrect'));
    });

    test('decodes field-level details on a VALIDATION_FAILED envelope', () {
      final json = <String, dynamic>{
        'success': false,
        'data': null,
        'error': {
          'code': 'VALIDATION_FAILED',
          'message': 'Some fields need your attention.',
          'details': [
            {'field': 'email', 'message': 'Enter a valid email address.'},
            {
              'field': 'password',
              'message': 'Password and confirmation do not match.',
            },
          ],
        },
        'meta': null,
      };

      final result = ApiResult<Object>.fromJson(json, (_) => Object());
      expect(result.error!.details, hasLength(2));
      expect(result.error!.details.first.field, equals('email'));
      expect(result.error!.details.last.field, equals('password'));
    });

    test('exposes pagination meta when present', () {
      final json = <String, dynamic>{
        'success': true,
        'data': <Map<String, dynamic>>[],
        'error': null,
        'meta': {
          'page': 1,
          'pageSize': 20,
          'totalItems': 137,
          'totalPages': 7,
        },
      };

      final result = ApiResult<List<Object>>.fromJson(
        json,
        (data) => (data! as List<dynamic>).cast<Object>(),
      );
      expect(result.meta!['totalItems'], equals(137));
      expect(result.meta!['totalPages'], equals(7));
    });

    test('tolerates missing fields (resilient to backend bugs)', () {
      final json = <String, dynamic>{'success': true};
      final result = ApiResult<int>.fromJson(json, (_) => 42);
      expect(result.success, isTrue);
      expect(result.data, equals(42));
      expect(result.error, isNull);
    });
  });

  group('ApiFailure.fromEnvelope', () {
    test('lifts code, message and details from an error envelope', () {
      const error = ApiResultError(
        code: 'AUTH_INVALID_CREDENTIALS',
        message: 'Wrong.',
        details: [
          ApiResultErrorDetail(field: 'email', message: 'bad'),
        ],
      );
      final failure = ApiFailure.fromEnvelope(error, httpStatus: 401);
      expect(failure.code, equals('AUTH_INVALID_CREDENTIALS'));
      expect(failure.message, equals('Wrong.'));
      expect(failure.httpStatus, equals(401));
      expect(failure.details, hasLength(1));
    });

    test('picks the localized message by isArabic (#8/#11)', () {
      const error = ApiResultError(
        code: 'AUTH_INVALID_CREDENTIALS',
        message: 'The email or password is incorrect.',
        messageArabic: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
      );
      expect(
        ApiFailure.fromEnvelope(error, isArabic: true).message,
        equals('البريد الإلكتروني أو كلمة المرور غير صحيحة.'),
      );
      expect(
        ApiFailure.fromEnvelope(error, isArabic: false).message,
        equals('The email or password is incorrect.'),
      );
    });

    test('falls back to English when the envelope carries no Arabic', () {
      const error = ApiResultError(code: 'X', message: 'Only English.');
      expect(
        ApiFailure.fromEnvelope(error, isArabic: true).message,
        equals('Only English.'),
      );
    });
  });
}
