import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/speaker_profile_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SpeakerDetail _detail({bool allowsMeeting = true, bool allowsData = true}) {
  final start = DateTime.utc(2026, 11, 23, 6);
  return SpeakerDetail(
    id: 'sp1',
    name: 'Capt. Reef',
    nameArabic: 'القبطان ريف',
    rank: 'Sea captain',
    allowsMeetingRequests: allowsMeeting,
    allowsDataSharing: allowsData,
    displayOrder: 0,
    bio: 'A maritime leader.',
    facebookUrl: allowsData ? 'https://fb/x' : null,
    sessions: <SpeakerSession>[
      SpeakerSession(
        id: 'se1',
        code: 'S-1',
        title: 'Opening talk',
        titleArabic: 'حديث',
        hallName: 'Main Hall',
        hallNameArabic: 'الرئيسية',
        startUtc: start,
        endUtc: start.add(const Duration(hours: 1)),
      ),
    ],
  );
}

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'v@x.sa',
      displayName: 'Visitor One',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _Guest extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

class _FakeRepo implements SpeakersRepository {
  _FakeRepo({this.detail, this.status});

  final SpeakerDetail? detail;
  final int? status;
  int submits = 0;

  @override
  Future<List<SpeakerSummary>> getSpeakers() => throw UnimplementedError();

  @override
  Future<SpeakerDetail> getSpeaker(String id) async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return detail!;
  }

  @override
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
  }) async {
    submits++;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required SpeakersRepository repo,
  required AuthController controller,
}) async {
  final router = GoRouter(
    initialLocation: '/speakers/sp1',
    routes: <RouteBase>[
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, state) => SpeakerProfileScreen(
          speakerId: state.pathParameters['speakerId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/sign-in',
        name: RouteNames.signIn,
        builder: (_, __) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, __) => const Scaffold(body: Text('SESSION')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        speakersRepositoryProvider.overrideWithValue(repo),
        authControllerProvider.overrideWith(() => controller),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SpeakerProfileScreen (Page 020)', () {
    testWidgets('renders the profile, CV, sessions + meeting button',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      expect(find.text('Capt. Reef'), findsOneWidget);
      expect(find.text('A maritime leader.'), findsOneWidget);
      expect(find.text('Opening talk'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Request meeting'), findsOneWidget);
    });

    testWidgets('a guest tapping Request meeting is sent to sign-in',
        (tester) async {
      await _pump(tester, repo: _FakeRepo(detail: _detail()), controller: _Guest());
      await tester.tap(find.widgetWithText(FilledButton, 'Request meeting'));
      await tester.pumpAndSettle();
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('a signed-in visitor can submit a meeting request',
        (tester) async {
      final repo = _FakeRepo(detail: _detail());
      await _pump(tester, repo: repo, controller: _SignedIn());

      await tester.tap(find.widgetWithText(FilledButton, 'Request meeting'));
      await tester.pumpAndSettle();
      // The sheet opened with the name prefilled + a subject field.
      expect(find.text('Subject'), findsOneWidget);
      await tester.enterText(find.byType(TextField).last, 'Discuss navigation');
      await tester.tap(find.widgetWithText(FilledButton, 'Send request'));
      await tester.pumpAndSettle();

      expect(repo.submits, 1);
      expect(find.text('Meeting request sent'), findsOneWidget);
    });

    testWidgets('no meeting button when the speaker opted out', (tester) async {
      await _pump(
        tester,
        repo: _FakeRepo(detail: _detail(allowsMeeting: false)),
        controller: _SignedIn(),
      );
      expect(find.widgetWithText(FilledButton, 'Request meeting'), findsNothing);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(tester, repo: _FakeRepo(status: 404), controller: _Guest());
      expect(find.text('This speaker was not found'), findsOneWidget);
    });
  });
}
