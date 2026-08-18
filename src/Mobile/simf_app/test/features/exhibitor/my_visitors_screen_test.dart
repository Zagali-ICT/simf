// D-426 — exhibitor "My Booth Visitors": empty state, list of captured
// visitors, and the 403 (not-an-exhibitor) surface. BUG-025 — the list also
// carries the "these are booth scans, not My Contacts" note so the two features
// are never confused.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/my_visitors_screen.dart';
import 'package:simf_app/features/exhibitor/widgets/captured_visitor_sheet.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

class _FakeExhibitorRepo implements ExhibitorRepository {
  _FakeExhibitorRepo(
      {List<ExhibitorVisitor> visitors = const <ExhibitorVisitor>[],
      this.status,})
      : visitors = <ExhibitorVisitor>[...visitors];

  final List<ExhibitorVisitor> visitors;
  final int? status;

  /// FR-EXH-002 — the ids the sheet asked to remove / export.
  final List<String> removed = <String>[];
  final List<String> exported = <String>[];

  @override
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) async =>
      throw UnimplementedError();

  @override
  Future<List<ExhibitorVisitor>> listMyVisitors() async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return visitors;
  }

  /// Behaves like the server: the lead leaves the booth's list, so the reload
  /// the screen does after a removal comes back without it.
  @override
  Future<void> removeVisitor(String id) async {
    removed.add(id);
    visitors.removeWhere((v) => v.id == id);
  }

  @override
  Future<String> getVcard(String id) async {
    exported.add(id);
    return 'BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Visitor One\r\nEND:VCARD\r\n';
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
    simfTestScope(
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
      scannedAt: DateTime.utc(2026),
      card: VisitorCard(
          userId: 'u-$name', name: name, nameArabic: name, available: true,),
    );

void main() {
  testWidgets('empty state when no visitors captured', (tester) async {
    await _pump(tester, _FakeExhibitorRepo());
    expect(
      find.text(
        'No booth visitors yet. Scan a visitor badge at your booth to capture '
        'them here.',
      ),
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

  // BUG-025 — the exhibitor list is titled for the booth and carries the
  // one-line note separating it from My Contacts.
  testWidgets('titles the booth and explains it is not My Contacts',
      (tester) async {
    await _pump(
      tester,
      _FakeExhibitorRepo(visitors: <ExhibitorVisitor>[_visitor('Visitor One')]),
    );
    expect(find.text('My Booth Visitors'), findsOneWidget);
    expect(find.byType(SimfPageNote), findsOneWidget);
    expect(
      find.text(
        'Badges you scanned at your booth. This list is separate from My '
        'Contacts.',
      ),
      findsOneWidget,
    );
  });

  testWidgets('a 403 (not an exhibitor) shows the forbidden message',
      (tester) async {
    await _pump(tester, _FakeExhibitorRepo(status: 403));
    expect(
      find.text('Only exhibitor accounts can scan visitor badges.'),
      findsOneWidget,
    );
  });

  // FR-EXH-002 — the lead list had NEITHER a remove nor an export while My
  // Contacts has had both since D-286: a mis-scan was permanent and the card
  // could only be read on screen. Tapping a row now opens the sheet carrying
  // both actions.
  testWidgets('FR-EXH-002: tapping a lead opens the export + remove sheet',
      (tester) async {
    await _pump(
      tester,
      _FakeExhibitorRepo(visitors: <ExhibitorVisitor>[_visitor('Visitor One')]),
    );

    await tester.tap(find.byType(ContactCard));
    await tester.pumpAndSettle();

    expect(find.byType(CapturedVisitorSheet), findsOneWidget);
    expect(find.text('Export vCard'), findsOneWidget);
    expect(find.text('Remove'), findsOneWidget);
  });

  testWidgets('FR-EXH-002: a confirmed removal drops the lead and reloads',
      (tester) async {
    final repo = _FakeExhibitorRepo(
      visitors: <ExhibitorVisitor>[
        _visitor('Visitor One'),
        _visitor('Visitor Two'),
      ],
    );
    await _pump(tester, repo);

    await tester.tap(find.byType(ContactCard).first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Remove'));
    await tester.pumpAndSettle();

    // The destructive action is confirmed first — a lead carries the visitor's
    // consent trail, so it is never dropped on a stray tap.
    expect(find.text('Remove this visitor?'), findsOneWidget);
    await tester.tap(find.text('Remove').last);
    await tester.pumpAndSettle();

    expect(repo.removed, <String>['v-Visitor One']);
    expect(find.text('Visitor removed'), findsOneWidget);
    // Reloaded from the server, so the dropped lead is gone from the list.
    expect(find.text('Visitor One'), findsNothing);
    expect(find.text('Visitor Two'), findsOneWidget);
  });
}
