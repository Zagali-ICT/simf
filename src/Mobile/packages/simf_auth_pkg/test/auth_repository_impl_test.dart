import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _MockAuthApi extends Mock implements AuthApi {}

void main() {
  setUpAll(() {
    registerFallbackValue(
      const SignInRequest(email: '', password: ''),
    );
    registerFallbackValue(
      const VerifyEmailRequest(email: '', code: ''),
    );
    registerFallbackValue(
      const RefreshRequest(refreshToken: ''),
    );
  });

  group('AuthRepositoryImpl.signIn', () {
    test('returns SignInSession when the API returns a token payload',
        () async {
      final api = _MockAuthApi();
      when(() => api.signIn(any())).thenAnswer((_) async {
        return TokenResponseData(
          payload: TokenPayloadDto(
            accessToken: 'A',
            refreshToken: 'R',
            tokenType: 'Bearer',
            accessTokenExpiresInSeconds: 1800,
            user: const CurrentUserDto(
              id: 'u-1',
              email: 'r.alsalem@example.sa',
              displayName: 'Raed',
              appRole: 'Visitor',
              preferredLanguage: 'ar',
              registrationStatus: 'Approved',
            ),
          ),
        );
      });

      final frozenNow = DateTime.utc(2026, 5, 29, 12);
      final repo = AuthRepositoryImpl(api: api, now: () => frozenNow);
      final result = await repo.signIn(
        email: 'r.alsalem@example.sa',
        password: 'pw',
      );

      expect(result, isA<SignInSession>());
      final session = (result as SignInSession).session;
      expect(session.accessToken, equals('A'));
      expect(session.refreshToken, equals('R'));
      expect(
        session.accessTokenExpiresAt,
        equals(frozenNow.add(const Duration(seconds: 1800))),
      );
      expect(session.user.appRole, equals(AppRole.visitor));
      expect(
        session.user.registrationStatus,
        equals(RegistrationStatus.approved),
      );
    });

    test('returns SignInChallenge when the API returns mfaRequired',
        () async {
      final api = _MockAuthApi();
      when(() => api.signIn(any())).thenAnswer(
        (_) async => const MfaChallengeResponseData(mfaToken: 'mfa-123'),
      );

      final repo = AuthRepositoryImpl(api: api);
      final result = await repo.signIn(email: 'admin@simf', password: 'pw');

      expect(result, isA<SignInChallenge>());
      expect((result as SignInChallenge).mfaToken, equals('mfa-123'));
    });

    test('translates AUTH_INVALID_CREDENTIALS into InvalidCredentials',
        () async {
      final api = _MockAuthApi();
      when(() => api.signIn(any())).thenThrow(
        const ApiFailure(
          code: ApiErrorCodes.authInvalidCredentials,
          message: 'Wrong.',
          httpStatus: 401,
        ),
      );

      final repo = AuthRepositoryImpl(api: api);
      await expectLater(
        repo.signIn(email: 'a@b', password: 'wrong'),
        throwsA(isA<InvalidCredentials>()),
      );
    });

    test('translates network errors into NetworkUnavailable', () async {
      final api = _MockAuthApi();
      when(() => api.signIn(any())).thenThrow(
        const ApiFailure(
          code: ApiErrorCodes.clientNetwork,
          message: 'No internet',
        ),
      );

      final repo = AuthRepositoryImpl(api: api);
      await expectLater(
        repo.signIn(email: 'a@b', password: 'pw'),
        throwsA(isA<NetworkUnavailable>()),
      );
    });
  });

  group('AuthRepositoryImpl.signUp', () {
    test('passes confirmPassword through to the API', () async {
      final api = _MockAuthApi();
      when(() => api.signUp(any())).thenAnswer((_) async => const {});

      final repo = AuthRepositoryImpl(api: api);
      await repo.signUp(
        email: 'new@example.sa',
        password: 'Password1',
        confirmPassword: 'Password1',
      );

      final captured = verify(() => api.signUp(captureAny())).captured;
      final request = captured.single as SignUpRequest;
      expect(request.email, equals('new@example.sa'));
      expect(request.password, equals('Password1'));
      expect(request.confirmPassword, equals('Password1'));
    });

    test('translates AUTH_EMAIL_ALREADY_REGISTERED', () async {
      final api = _MockAuthApi();
      when(() => api.signUp(any())).thenThrow(
        const ApiFailure(
          code: ApiErrorCodes.authEmailAlreadyRegistered,
          message: 'taken',
          httpStatus: 409,
        ),
      );

      final repo = AuthRepositoryImpl(api: api);
      await expectLater(
        repo.signUp(
          email: 'taken@example.sa',
          password: 'x',
          confirmPassword: 'x',
        ),
        throwsA(isA<EmailAlreadyRegistered>()),
      );
    });
  });
}
