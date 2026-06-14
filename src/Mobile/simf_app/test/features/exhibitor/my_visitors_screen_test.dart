// D-426 — exhibitor "My Visitors": empty state, list of captured visitors,
// and the 403 (not-an-exhibitor) surface.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/my_visitors_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeExhibitorRepo implements ExhibitorRepository {
  _FakeExhibitorRepo({this.visitors = const <ExhibitorVisitor>[], this.status});

  final List<ExhibitorVisitor> visitors;
  final int? status;

  @override
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) async =>
      throw UnimplementedError();

  @override
  Future<List<ExhibitorVisitor>> listMyVisitors() async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork, message: 'x', httpStatus: status,
      );
    }
    return visitors;
  }
}

Future<void> _pump(WidgetTester tester, _FakeExhibitorRepo repo) async {
  final router = GoRouter(
    initialLocation: '/exhibitor/visitors',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.myVisitors,
        path: '/exhibitor/visitors',
        builder: (c, s) => const MyVisitorsScreen(),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
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

ExhibitorVisitor _visitor(String name) => ExhibitorVisitor(
      id: 'v-$name',
      scannedAt: DateTime.utc(2026, 1, 1),
      card: VisitorCard(userId: 'u-$name', name: name, nameArabic: name, available: true),
    );

void main() {
  testWidgets('empty state when no visitors captured', (tester) async {
    await _pump(tester, _FakeExhibitorRepo());
    expect(
      find.text('No visitors yet. Scan a visitor badge to capture them here.'),
      findsOneWidget,
    );
    expect(find.byType(ContactCard), findsNothing);
  });

  testWidgets('lists captured visitors', (tester) async {
    await _pump(
      tester,
      _FakeExhibitorRepo(
        visitors: <ExhibitorVisitor>[
          _visitor('Visitor One'),
          _visitor('Visitor Two'),
        ],
      ),
    );
    expect(find.byType(ContactCard), findsNWidgets(2));
    expect(find.text('Visitor One'), findsOneWidget);
    expect(find.text('Visitor Two'), findsOneWidget);
  });

  testWidgets('a 403 (not an exhibitor) shows the forbidden message',
      (tester) async {
    await _pump(tester, _FakeExhibitorRepo(status: 403));
    expect(
      find.text('Only exhibitor accounts can scan visitor badges.'),
      findsOneWidget,
    );
  });
}
