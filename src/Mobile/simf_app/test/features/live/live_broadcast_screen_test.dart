import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/live_broadcast_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

LiveSession _liveSession({
  String? liveStreamUrl,
  String? liveSignLanguageUrl,
  bool hasRecording = false,
  int status = 0,
}) =>
    LiveSession(
      title: 'Opening',
      titleArabic: 'الافتتاح',
      status: status,
      hasRecording: hasRecording,
      liveStreamUrl: liveStreamUrl,
      liveSignLanguageUrl: liveSignLanguageUrl,
    );

class _FakeLiveRepo implements LiveRepository {
  _FakeLiveRepo({this.session, this.status});

  final LiveSession? session;
  final int? status;
  int calls = 0;

  @override
  Future<LiveSession> getLiveSession(String sessionId) async {
    calls++;
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return session!;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required LiveRepository repo,
  String? sessionId,
  Locale locale = const Locale('en'),
  bool settle = true,
}) async {
  final router = GoRouter(
    initialLocation: '/live',
    routes: <RouteBase>[
      GoRoute(
        path: '/live',
        builder: (_, state) => LiveBroadcastScreen(sessionId: sessionId),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        liveRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    // A live feed initialises a player off the platform channel (absent here);
    // pump a few fixed frames to let the read + the (failing, headless) player
    // init resolve, instead of waiting on pumpAndSettle.
    for (var i = 0; i < 4; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }
  }
}

void main() {
  group('LiveBroadcastScreen (Page 025)', () {
    testWidgets('no sessionId shows the pick-a-session empty state',
        (tester) async {
      final repo = _FakeLiveRepo(session: _liveSession());
      await _pump(tester, repo: repo, sessionId: null);

      expect(
        find.text('No live session selected — open a session to watch.'),
        findsOneWidget,
      );
      // With no id the screen never fetches.
      expect(repo.calls, 0);
    });

    testWidgets('an empty sessionId is treated as no selection', (tester) async {
      final repo = _FakeLiveRepo(session: _liveSession());
      await _pump(tester, repo: repo, sessionId: '   ');

      expect(
        find.text('No live session selected — open a session to watch.'),
        findsOneWidget,
      );
      expect(repo.calls, 0);
    });

    testWidgets('no stream + no recording shows the not-live state',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(),
        ),
        sessionId: 's1',
      );

      expect(find.text('Opening'), findsOneWidget);
      expect(
        find.text('This session is not broadcasting right now.'),
        findsOneWidget,
      );
    });

    testWidgets('no stream but a recording shows the recording note',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(hasRecording: true),
        ),
        sessionId: 's1',
      );

      expect(
        find.text('A recording of this session is available.'),
        findsOneWidget,
      );
    });

    testWidgets('a sign-language url shows the sign-language note',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            hasRecording: true,
            liveSignLanguageUrl: 'https://stream.example.sa/sign.m3u8',
          ),
        ),
        sessionId: 's1',
      );

      expect(
        find.text('Sign-language interpretation is available.'),
        findsOneWidget,
      );
    });

    testWidgets('both feeds set shows the main / sign-language toggle',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            liveSignLanguageUrl: 'https://live.example.sa/sign.m3u8',
          ),
        ),
        sessionId: 's1',
        // The HLS feed can't initialise headless (no platform channel) — the
        // player surfaces the error state; settle:false avoids waiting on the
        // async init. The toggle is what we assert here.
        settle: false,
      );

      expect(find.text('Main feed'), findsOneWidget);
      expect(find.text('Sign language'), findsOneWidget);
    });

    testWidgets('a single (main-only) feed shows no toggle', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
          ),
        ),
        sessionId: 's1',
        settle: false,
      );

      expect(find.text('Sign language'), findsNothing);
    });

    testWidgets('an unplayable feed surfaces the error state with a retry',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
          ),
        ),
        sessionId: 's1',
        // video_player can't initialise headless → the player shows the terminal
        // error surface (not an endless spinner), with a Retry (D-349 / L-7).
        settle: false,
      );

      expect(find.text('Could not play this feed. Try again.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(status: 404),
        sessionId: 's1',
      );

      expect(find.text('This session was not found'), findsOneWidget);
    });

    testWidgets('a non-404 failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeLiveRepo(status: 500);
      await _pump(tester, repo: repo, sessionId: 's1');

      expect(
        find.text('Could not load the live broadcast.'),
        findsOneWidget,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });

    testWidgets('the not-live note is bilingual (Arabic)', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(session: _liveSession()),
        sessionId: 's1',
        locale: const Locale('ar'),
      );

      expect(find.text('الافتتاح'), findsOneWidget);
    });
  });
}
