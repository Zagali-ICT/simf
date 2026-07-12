import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/live/data/live_repository.dart';
import 'package:simf_app/features/live/live_broadcast_screen.dart';
import 'package:simf_app/features/live/widgets/live_badges.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

LiveSession _liveSession({
  String? liveStreamUrl,
  String? liveSignLanguageUrl,
  bool hasRecording = false,
  int status = 0,
  String? liveCaptions,
  String? liveCaptionsArabic,
  DateTime? startUtc,
  DateTime? endUtc,
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
      startUtc: startUtc,
      endUtc: endUtc,
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

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'v@x.sa',
      displayName: 'Visitor One',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _authSession() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

/// Default test auth = an approved, signed-in visitor (the live screen is
/// login-only under D-577, so the player-path tests must be signed in).
class _SignedIn extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_authSession());
}

/// A signed-out guest — used to assert the D-577 need-login gate.
class _Guest extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

Future<GoRouter> _pump(
  WidgetTester tester, {
  required LiveRepository repo,
  String? sessionId,
  OrgProfile? profile,
  Locale locale = const Locale('en'),
  bool settle = true,
  AuthController? auth,
  SimfPrefsStorage? prefs,
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
      // Navigating here disposes the live screen so the D-712 after-watch prompt
      // can fire; the /rate landing echoes its query so the prompt is observable.
      GoRoute(
        path: '/gone',
        builder: (_, __) => const Scaffold(body: Text('GONE')),
      ),
      GoRoute(
        path: '/rate',
        name: RouteNames.rate,
        builder: (_, state) => Scaffold(
          body: Text('RATE ${state.uri.queryParameters['targetId']} '
              '${state.uri.queryParameters['code']}'),
        ),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        liveRepositoryProvider.overrideWithValue(repo),
        orgProfileProvider.overrideWith(() => _StubOrgProfile(profile)),
        authControllerProvider.overrideWith(() => auth ?? _SignedIn()),
        simfPrefsStorageProvider.overrideWithValue(prefs ?? FakePrefs()),
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
  return router;
}

void main() {
  group('LiveBroadcastScreen (Page 025)', () {
    testWidgets('a signed-out guest sees the need-login gate, not the stream '
        '(owner, D-577)', (tester) async {
      final repo = _FakeLiveRepo(
        session: _liveSession(
          liveStreamUrl: 'https://www.youtube.com/watch?v=simf',
        ),
      );
      await _pump(tester, repo: repo, sessionId: 's1', auth: _Guest());

      // The guest sees the login prompt + a Sign-in button — never the player.
      expect(find.text('Sign in to watch the live stream.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
      // The stream/content is not shown, and the session is never fetched.
      expect(
        find.textContaining(
          'Live broadcasting is available only inside the Riyadh region',
        ),
        findsNothing,
      );
      expect(repo.calls, 0);
    });

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

    testWidgets('a not-live session renders the region notice but HIDES the '
        'ask-question entry (#7)', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(session: _liveSession()),
        sessionId: 's1',
      );

      // The static region-restriction notice card (frame 934:3619) always shows.
      expect(
        find.textContaining(
          'Live broadcasting is available only inside the Riyadh region',
        ),
        findsOneWidget,
      );
      // #7 (owner) — the ask entry shows only while the session is actually LIVE;
      // a not-live / recording view (a YouTube archive) offers no ask. (A live
      // session WITH the ask entry is locked by the live-broadcast golden.)
      expect(find.text('Ask a question'), findsNothing);
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
      // #7 (owner) — no ask on the post-session recording view (a YouTube
      // archive is not a live broadcast).
      expect(find.text('Ask a question'), findsNothing);
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

    testWidgets('D-712 — leaving a watched live session opens the rate screen '
        'once (shared dedup with the after-view prompt)', (tester) async {
      final router = await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
          ),
        ),
        sessionId: 's1',
        // The player can't init headless — settle:false; the after-watch prompt
        // is captured in _load regardless of the player.
        settle: false,
      );

      // Leaving the live screen opens the dynamic rate screen for THIS session.
      // The player is disposed on leave, so pumpAndSettle is safe here.
      router.go('/gone');
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsOneWidget);

      // Re-enter + leave again → not prompted a second time (the shared tracker
      // already recorded it). The re-mount re-inits the (headless-failing) player,
      // so pump fixed frames rather than settling on it.
      router.go('/live');
      await tester.pump(const Duration(milliseconds: 200));
      router.go('/gone');
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsNothing);
      expect(find.text('GONE'), findsOneWidget);
    });

    testWidgets('D-712 — a non-live session (no stream) does not prompt on leave',
        (tester) async {
      final router = await _pump(
        tester,
        repo: _FakeLiveRepo(session: _liveSession()), // no liveStreamUrl
        sessionId: 's1',
      );

      router.go('/gone');
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.text('RATE s1 Session'), findsNothing);
    });

    testWidgets('D-712 — a signed-out guest is never prompted on leave',
        (tester) async {
      final router = await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://www.youtube.com/watch?v=simf',
          ),
        ),
        sessionId: 's1',
        auth: _Guest(),
      );

      router.go('/gone');
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.text('RATE s1 Session'), findsNothing);
    });

    testWidgets('S-3 — a URL set but the session has NOT started (future) hides '
        'the LIVE badge and the Ask button', (tester) async {
      final now = DateTime.now().toUtc();
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            startUtc: now.add(const Duration(hours: 1)),
            endUtc: now.add(const Duration(hours: 2)),
          ),
        ),
        sessionId: 's1',
        settle: false,
      );

      // A premiere URL is up but the window has not opened → nothing lies live.
      expect(find.byType(LiveBadge), findsNothing);
      expect(find.text('Ask a question'), findsNothing);
      expect(find.text('Now broadcasting'), findsNothing);
    });

    testWidgets('S-3 — an in-window session (start<now<end) with a URL shows the '
        'LIVE badge, the Ask button and the now-broadcasting header',
        (tester) async {
      final now = DateTime.now().toUtc();
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            startUtc: now.subtract(const Duration(hours: 1)),
            endUtc: now.add(const Duration(hours: 1)),
          ),
        ),
        sessionId: 's1',
        settle: false,
      );

      expect(find.byType(LiveBadge), findsOneWidget);
      expect(find.text('Ask a question'), findsOneWidget);
      // "يُبث الآن" — asserted in the default English locale as its translation.
      expect(find.text('Now broadcasting'), findsOneWidget);
    });

    testWidgets('S-3 — an ENDED session (end<now) with the URL still set hides '
        'the LIVE badge + Ask and does NOT say now-broadcasting', (tester) async {
      final now = DateTime.now().toUtc();
      await _pump(
        tester,
        repo: _FakeLiveRepo(
          session: _liveSession(
            liveStreamUrl: 'https://live.example.sa/main.m3u8',
            startUtc: now.subtract(const Duration(hours: 3)),
            endUtc: now.subtract(const Duration(hours: 2)),
          ),
        ),
        sessionId: 's1',
        settle: false,
      );

      expect(find.byType(LiveBadge), findsNothing);
      expect(find.text('Ask a question'), findsNothing);
      // The archive header reads the neutral session label, not "now broadcasting".
      expect(find.text('Now broadcasting'), findsNothing);
      expect(find.text('Session'), findsOneWidget);
    });

    testWidgets('S-3 — the global main-live (no window) still reads live: the '
        'LIVE badge shows', (tester) async {
      await _pump(
        tester,
        repo: _FakeLiveRepo(session: _liveSession()),
        sessionId: null,
        profile: _orgProfile(
          liveStreamUrl: 'https://www.youtube.com/watch?v=simf',
        ),
        settle: false,
      );

      // No start/end on the synthetic session → fall back to "a feed is up".
      expect(find.byType(LiveBadge), findsOneWidget);
      expect(find.text('Now broadcasting'), findsOneWidget);
    });
  });
}
