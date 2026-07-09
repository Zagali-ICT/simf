@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/live_broadcast_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../features/accessibility/_fake_prefs.dart';
import 'golden_fonts.dart';

/// Golden render of the Live-broadcast screen against Figma frame **934:3450**.
///   flutter test --update-goldens test/golden/live_broadcast_golden_test.dart
///
/// **Player env-limit (documented):** the video player (YouTube IFrame /
/// `video_player`) has no headless platform in a widget test, so the central
/// player box renders the error surface (the controller build throws and is
/// caught) — the golden's parity claim is the *chrome*
/// (the LIVE badge + language chip + AI caption strip) and the *info column*
/// below it (يُبث الآن header, the gold session-title + speakers bullets, the
/// gold region-restriction notice, and the الجلسات القادمة upcoming cards),
/// which all render deterministically. The player init is left to `pump` fixed
/// frames (never `pumpAndSettle` — the feed never settles).

class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u1',
            email: 'v@example.sa',
            displayName: 'V',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('ar'),
            registrationStatus: RegistrationStatus.approved,
          ),
        ),
      );
}

LiveSession _liveSession() => const LiveSession(
      title: 'Opening session: The Future of Maritime Security',
      titleArabic: 'الجلسة الافتتاحية: مستقبل الأمن البحري',
      status: 1,
      hasRecording: false,
      liveStreamUrl: 'https://www.youtube.com/watch?v=simf',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      speakers: <LiveSpeaker>[
        LiveSpeaker(
          name: 'Defence Minister',
          nameArabic: 'معالي وزير الدفاع · رئيس أركان البحرية الملكية السعودية',
        ),
      ],
    );

final _upcoming = <UpcomingSession>[
  UpcomingSession(
    id: 'u1',
    title: 'General Authority for Defence Development',
    titleArabic: 'الهيئة العامة للتطوير الدفاعي',
    startUtc: DateTime(2026, 11, 23, 11),
  ),
  UpcomingSession(
    id: 'u2',
    title: 'Securing International Waterways',
    titleArabic: 'تأمين الممرات المائية الدولية',
    startUtc: DateTime(2026, 11, 23, 12),
  ),
];

class _FakeLiveRepo implements LiveRepository {
  @override
  Future<LiveSession> getLiveSession(String sessionId) async => _liveSession();

  @override
  Future<List<UpcomingSession>> getUpcomingSessions({
    String? excludeSessionId,
    int take = 3,
  }) async =>
      _upcoming;
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Live-broadcast @375x900 — Figma 934:3450 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/live?sessionId=s1',
      routes: <RouteBase>[
        GoRoute(
          path: '/live',
          name: RouteNames.liveBroadcast,
          builder: (_, state) => LiveBroadcastScreen(
            sessionId: state.uri.queryParameters['sessionId'],
          ),
        ),
        GoRoute(
          path: '/live/question',
          name: RouteNames.sendQuestion,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        // D-712 — the after-watch rate prompt pushes /rate when the screen is
        // disposed at teardown; declare the route so that push resolves harmlessly
        // (the golden is captured before dispose, so this never affects the image).
        GoRoute(
          path: '/rate',
          name: RouteNames.rate,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(_SignedIn.new),
          liveRepositoryProvider.overrideWithValue(_FakeLiveRepo()),
          // D-712 — the screen now reads the rate-prompt tracker (prefs-backed)
          // for an eligible attendee; the golden must supply the prefs override.
          simfPrefsStorageProvider.overrideWithValue(FakePrefs()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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
    // pump fixed frames (NOT pumpAndSettle — the headless player never settles).
    for (var i = 0; i < 5; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }

    await expectLater(
      find.byType(LiveBroadcastScreen),
      matchesGoldenFile('goldens/live_broadcast_934-3450.png'),
    );
  });
}
