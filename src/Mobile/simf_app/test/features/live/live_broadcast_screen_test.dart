import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/live_broadcast_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

LiveSession _liveSession({
  String? liveStreamUrl,
  String? liveSignLanguageUrl,
  bool hasRecording = false,
  int status = 0,
  String? liveCaptions,
  String? liveCaptionsArabic,
}) =>
    LiveSession(
      title: 'Opening',
      titleArabic: 'الافتتاح',
      status: status,
      hasRecording: hasRecording,
      liveStreamUrl: liveStreamUrl,
      liveSignLanguageUrl: liveSignLanguageUrl,
      liveCaptions: liveCaptions,
      liveCaptionsArabic: liveCaptionsArabic,
    );

class _FakeLiveRepo implements LiveRepository {
  _FakeLiveRepo({this.session, this.status, this.upcoming = const <UpcomingSession>[]});

  final LiveSession? session;
  final int? status;
  final List<UpcomingSession> upcoming;
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

  @override
  Future<List<UpcomingSession>> getUpcomingSessions({
    String? excludeSessionId,
    int take = 3,
  }) async =>
      upcoming;
}

/// Pins the shared org-profile value (null by default → the no-session screen
/// shows the empty state; a profile with a liveStreamUrl → the global main-live).
class _StubOrgProfile extends OrgProfileController {
  _StubOrgProfile(this._value);
  final OrgProfile? _value;
  @override
  OrgProfile? build() => _value;
  @override
  Future<void> warm() async {}
}

OrgProfile _orgProfile({String? liveStreamUrl}) => OrgProfile(
      name: 'The Forum',
      nameArabic: 'الملتقى',
      title: 'The Forum',
      titleArabic: 'الملتقى',
      currentYear: 2026,
      status: 'Open',
      social: const OrgSocial(),
      aboutItems: const <OrgAboutItem>[],
      details: const <OrgDetail>[],
      liveStreamUrl: liveStreamUrl,
    );

Future<void> _pump(
  WidgetTester tester, {
  required LiveRepository repo,
  String? sessionId,
  OrgProfile? profile,
  Locale locale = const Locale('en'),
  bool settle = true,
}) async {
  // Tall surface so the whole lazy scroll (player band → title → region notice →
  // ask-question, or the not-live message) lays out in the test viewport.
  tester.view.physicalSize = const Size(1200, 2600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
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
        orgProfileProvider.overrideWith(() => _StubOrgProfile(profile)),
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

    testWidgets('D-495 — no sessionId but a profile live link plays the global '
        'main live (forum name, no ask-question)', (tester) async {
      final repo = _FakeLiveRepo(session: _liveSession());
      await _pump(
        tester,
        repo: repo,
        sessionId: null,
        profile: _orgProfile(
          liveStreamUrl: 'https://www.youtube.com/watch?v=simf',
        ),
        // The player can't init headless — the region notice + title still render.
        settle: false,
      );

      // Not the empty state — the global main-live is shown instead.
      expect(
        find.text('No live session selected — open a session to watch.'),
        findsNothing,
      );
      // The forum name is the now-broadcasting title.
      expect(find.text('The Forum'), findsOneWidget);
      // The static region-restriction notice still renders.
      expect(
        find.textContaining(
          'Live broadcasting is available only inside the Riyadh region',
        ),
        findsOneWidget,
      );
      // The session-specific ask-question entry is hidden for the global live.
      expect(find.text('Ask a question'), findsNothing);
      // No session id → never fetched a session.
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

    testWidgets('the loaded content renders the region notice + ask-question',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(session: _liveSession()),
        sessionId: 's1',
      );

      // The static region-restriction notice card (frame 934:3619).
      expect(
        find.textContaining(
          'Live broadcasting is available only inside the Riyadh region',
        ),
        findsOneWidget,
      );
      // The ask-a-question entry to Page 026.
      expect(find.text('Ask a question'), findsOneWidget);
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

    testWidgets('renders the hall name + speakers line + upcoming sessions '
        '(D-433)', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: const LiveSession(
            title: 'Opening',
            titleArabic: 'الافتتاح',
            status: 1,
            hasRecording: false,
            hallName: 'Main Hall',
            hallNameArabic: 'القاعة الرئيسية',
            speakers: <LiveSpeaker>[
              LiveSpeaker(name: 'Capt. Reef', nameArabic: 'القبطان'),
            ],
          ),
          upcoming: <UpcomingSession>[
            UpcomingSession(
              id: 's2',
              title: 'Next talk',
              titleArabic: 'الجلسة التالية',
              startUtc: DateTime(2030, 1, 1, 11),
            ),
          ],
        ),
        sessionId: 's1',
      );

      // Hall name completes the "Session · Main Hall" header line.
      expect(find.textContaining('Main Hall'), findsOneWidget);
      // The speakers / participants line.
      expect(find.text('Capt. Reef'), findsOneWidget);
      // The upcoming-sessions section + its card + the gold time chip.
      expect(find.text('Upcoming sessions'), findsOneWidget);
      expect(find.text('Next talk'), findsOneWidget);
      expect(find.text('11:00'), findsOneWidget);
    });

    testWidgets('P5 — a session with caption text shows it in the caption strip '
        '(white)', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            liveCaptions: 'Welcome to the opening session.',
          ),
        ),
        sessionId: 's1',
        // The HLS feed can't initialise headless — the player surfaces its error
        // state, but the caption strip is a sibling and still renders.
        settle: false,
      );

      final caption = find.text('Welcome to the opening session.');
      expect(caption, findsOneWidget);
      // The placeholder hint is NOT shown when a real caption is present.
      expect(
        find.text('Live captions of the spoken word appear here…'),
        findsNothing,
      );
      // Real caption text reads in the surface (white) token — not the muted
      // placeholder colour. Assert the token so a re-tint can't silently pass.
      expect(tester.widget<Text>(caption).style!.color, SimfTokens.surface);
    });

    testWidgets('P5 — a live session with no caption shows the placeholder hint',
        (tester) async {
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

      final hint = find.text('Live captions of the spoken word appear here…');
      expect(hint, findsOneWidget);
      // The placeholder reads in the frame's soft caption colour (#DDE4F0,
      // 934:3613) — assert the token so a re-tint can't silently pass.
      expect(tester.widget<Text>(hint).style!.color, SimfTokens.captionText);
    });

    testWidgets('P5 — the caption renders the Arabic text under the ar locale',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            liveCaptions: 'English caption.',
            liveCaptionsArabic: 'الترجمة العربية.',
          ),
        ),
        sessionId: 's1',
        locale: const Locale('ar'),
        settle: false,
      );

      expect(find.text('الترجمة العربية.'), findsOneWidget);
      expect(find.text('English caption.'), findsNothing);
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
