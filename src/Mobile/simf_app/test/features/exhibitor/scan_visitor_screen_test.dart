// D-426 — exhibitor "Scan visitor badge": the manual-entry → scanByBadge →
// capture/route (success) and the 404 / 403 / generic-error toast branches.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/scan_visitor_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Records the scanned code and either returns a captured card or throws the
/// configured `ApiFailure` — so the screen's `_onCode` branches are testable.
class _FakeExhibitorRepo implements ExhibitorRepository {
  _FakeExhibitorRepo({this.status});

  final int? status;
  String? lastScanned;
  int scanCalls = 0;

  @override
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) async {
    scanCalls++;
    lastScanned = qrId;
    final s = status;
    if (s != null) {
      throw ApiFailure(code: 'X', message: 'x', httpStatus: s);
    }
    return const VisitorCard(
      userId: 'u1',
      name: 'Visitor',
      nameArabic: 'زائر',
      available: true,
    );
  }

  @override
  Future<List<ExhibitorVisitor>> listMyVisitors() async =>
      const <ExhibitorVisitor>[];

  // FR-EXH-002 — the lead-list actions; the scanner never calls either.
  @override
  Future<void> removeVisitor(String id) async => throw UnimplementedError();

  @override
  Future<String> getVcard(String id) async => throw UnimplementedError();
}

Future<void> _pump(WidgetTester tester, _FakeExhibitorRepo repo) async {
  final router = GoRouter(
    initialLocation: '/exhibitor/scan',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.scanVisitor,
        path: '/exhibitor/scan',
        builder: (c, s) => const ScanVisitorScreen(enableCamera: false),
      ),
      GoRoute(
        name: RouteNames.myVisitors,
        path: '/exhibitor/visitors',
        builder: (c, s) => const Scaffold(body: Text('MY VISITORS')),
      ),
      GoRoute(
        name: RouteNames.badge,
        path: '/badge',
        builder: (c, s) => const Scaffold(body: Text('BADGE')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        exhibitorRepositoryProvider.overrideWithValue(repo),
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

Future<void> _scan(WidgetTester tester, String code) async {
  await tester.enterText(find.byType(TextField).first, code);
  await tester.tap(find.widgetWithText(FilledButton, 'Look up'));
  await tester.pumpAndSettle();
}

void main() {
  group('ScanVisitorScreen (D-426)', () {
    testWidgets('a valid badge captures the visitor + routes to My Visitors',
        (tester) async {
      final repo = _FakeExhibitorRepo();
      await _pump(tester, repo);

      await _scan(tester, ' V-123 ');

      expect(repo.scanCalls, 1);
      // The code is trimmed before the call.
      expect(repo.lastScanned, 'V-123');
      // Success routes to the My Visitors list.
      expect(find.text('MY VISITORS'), findsOneWidget);
    });

    testWidgets('an unknown badge (404) shows the not-found toast + stays',
        (tester) async {
      final repo = _FakeExhibitorRepo(status: 404);
      await _pump(tester, repo);

      await _scan(tester, 'NOPE');

      expect(find.text('No matching visitor badge'), findsOneWidget);
      expect(find.text('MY VISITORS'), findsNothing);
    });

    testWidgets('a non-exhibitor (403) shows the forbidden toast + stays',
        (tester) async {
      final repo = _FakeExhibitorRepo(status: 403);
      await _pump(tester, repo);

      await _scan(tester, 'CODE');

      expect(
        find.text('Only exhibitor accounts can scan visitor badges.'),
        findsOneWidget,
      );
      expect(find.text('MY VISITORS'), findsNothing);
    });

    testWidgets('a transport / 5xx failure shows the generic error toast',
        (tester) async {
      final repo = _FakeExhibitorRepo(status: 500);
      await _pump(tester, repo);

      await _scan(tester, 'CODE');

      expect(find.text('Could not scan the badge. Try again.'), findsOneWidget);
      expect(find.text('MY VISITORS'), findsNothing);
    });
  });
}
