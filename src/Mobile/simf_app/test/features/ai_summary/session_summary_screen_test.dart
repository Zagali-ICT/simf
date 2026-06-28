import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/ai_summary/data/session_summary_models.dart';
import 'package:simf_app/features/ai_summary/data/session_summary_repository.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SessionSummary _summary() => SessionSummary.fromJson(const <String, dynamic>{
      'keyPoints': 'Coral cover rising\nNew survey method',
      'keyPointsArabic': 'تحسن الغطاء المرجاني\nأسلوب مسح جديد',
      'recommendations': 'Scale the reef programme',
      'recommendationsArabic': 'توسيع برنامج الشعاب',
      'speakers': 'Dr Reef, Cmdr Tide',
      'speakersArabic': 'د. ريف، العميد تايد',
      'fullText': 'The session covered reef restoration progress.',
      'fullTextArabic': 'تناولت الجلسة تقدم استعادة الشعاب.',
      'generatedByAi': true,
      'publishedAt': '2026-11-23T07:30:00Z',
    });

SessionListItem _session(String id, String titleEn) =>
    SessionListItem.fromJson(<String, dynamic>{
      'id': id,
      'code': 'C$id',
      'title': titleEn,
      'titleArabic': 'جلسة $id',
      'hallId': 'h1',
      'hallName': 'Main Hall',
      'hallNameArabic': 'القاعة الرئيسية',
      'startUtc': '2026-11-23T07:30:00Z',
      'endUtc': '2026-11-23T08:30:00Z',
      'speakers': <dynamic>[],
    });

final _sessions = <SessionListItem>[
  _session('s1', 'Reef session'),
  _session('s2', 'Other session'),
];

class _FakeSummaryRepo implements SessionSummaryRepository {
  _FakeSummaryRepo({this.summary, this.status});

  final SessionSummary? summary;
  final int? status;
  int calls = 0;

  @override
  Future<SessionSummary> getSummary(String sessionId) async {
    calls++;
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return summary!;
  }
}

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Future<void> _pump(
  WidgetTester tester, {
  required SessionSummaryRepository repo,
  List<SessionListItem> sessions = const <SessionListItem>[],
  String? sessionId,
  Locale locale = const Locale('en'),
}) async {
  tester.view.physicalSize = const Size(375, 1800);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/ai-summary',
    routes: <RouteBase>[
      GoRoute(
        path: '/ai-summary',
        name: RouteNames.aiSummary,
        builder: (_, __) => AiSummaryScreen(sessionId: sessionId),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        sessionSummaryRepositoryProvider.overrideWithValue(repo),
        aiSummarySessionsProvider.overrideWith((ref) async => sessions),
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
  await tester.pumpAndSettle();
}

void main() {
  group('AiSummaryScreen (Page 034 — KSA frame 1072:13518)', () {
    testWidgets(
        'details: info card, tabs (Key points active), generate paragraph',
        (tester) async {
      final repo = _FakeSummaryRepo(summary: _summary());
      await _pump(tester, repo: repo, sessions: _sessions, sessionId: 's1');

      expect(repo.calls, 1);
      // The "الجلسة"/Session info-card label.
      expect(find.text('Session'), findsOneWidget);
      // No old picker / banner / action row.
      expect(find.text('Choose the session'), findsNothing);
      expect(find.text('Auto-generated summary'), findsNothing);
      expect(find.text('Share'), findsNothing);
      expect(find.text('Save'), findsNothing);
      // The default tab (Key points) shows its bullets; other tabs' content is
      // gated until selected.
      expect(find.text('Coral cover rising'), findsOneWidget);
      expect(find.text('New survey method'), findsOneWidget);
      expect(find.text('Scale the reef programme'), findsNothing);
      // The generate button + the published paragraph (expanded by default).
      expect(find.text('Generate session summary'), findsOneWidget);
      expect(
        find.text('The session covered reef restoration progress.'),
        findsOneWidget,
      );
    });

    testWidgets('tapping a tab switches the active content', (tester) async {
      final repo = _FakeSummaryRepo(summary: _summary());
      await _pump(tester, repo: repo, sessions: _sessions, sessionId: 's1');

      await tester.tap(find.text('Recommendations'));
      await tester.pumpAndSettle();
      expect(find.text('Scale the reef programme'), findsOneWidget);
      expect(find.text('Coral cover rising'), findsNothing);

      await tester.tap(find.text('Speakers'));
      await tester.pumpAndSettle();
      expect(find.text('Dr Reef, Cmdr Tide'), findsOneWidget);
    });

    testWidgets('the generate button collapses / expands the paragraph',
        (tester) async {
      final repo = _FakeSummaryRepo(summary: _summary());
      await _pump(tester, repo: repo, sessions: _sessions, sessionId: 's1');

      const paragraph = 'The session covered reef restoration progress.';
      expect(find.text(paragraph), findsOneWidget);
      await tester.tap(find.text('Generate session summary'));
      await tester.pumpAndSettle();
      expect(find.text(paragraph), findsNothing);
    });

    testWidgets('with no sessionId it auto-selects the first session',
        (tester) async {
      final repo = _FakeSummaryRepo(summary: _summary());
      await _pump(tester, repo: repo, sessions: _sessions);

      expect(repo.calls, 1);
      expect(find.text('Choose the session'), findsNothing);
      expect(find.text('Generate session summary'), findsOneWidget);
    });

    testWidgets('a 404 shows the no-published-summary note', (tester) async {
      await _pump(
        tester,
        repo: _FakeSummaryRepo(status: 404),
        sessions: _sessions,
        sessionId: 's1',
      );
      // The empty note appears in the tab content and the (expanded) paragraph.
      expect(find.text('No published summary yet.'), findsWidgets);
      expect(find.text('Choose the session'), findsNothing);
    });

    testWidgets('a non-404 failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeSummaryRepo(status: 500);
      await _pump(tester, repo: repo, sessions: _sessions, sessionId: 's1');

      expect(find.text('Could not load the summary.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
