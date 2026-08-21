// Regression guard for the busy flag on the handler that POPS the sheet.
// Same lifecycle as the My-Contacts sheet this one mirrors: `pop()` only
// reverses the route's animation controller, so the State survives — and
// `mounted` keeps returning true — for the whole 200ms exit transition, and
// clearing the flag there repaints a sheet the user can still see.
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/widgets/captured_visitor_sheet.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

class _FakeExhibitorRepo implements ExhibitorRepository {
  _FakeExhibitorRepo({this.removeStatus});

  final int? removeStatus;
  int removeCalls = 0;

  @override
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) async =>
      throw UnimplementedError();

  @override
  Future<List<ExhibitorVisitor>> listMyVisitors() async =>
      const <ExhibitorVisitor>[];

  @override
  Future<void> removeVisitor(String id) async {
    removeCalls++;
    if (removeStatus != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: removeStatus,
      );
    }
  }

  @override
  Future<String> getVcard(String id) async =>
      'BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Visitor One\r\nEND:VCARD\r\n';
}

const _removeLabel = 'Remove';

ExhibitorVisitor _visitor() => ExhibitorVisitor(
      id: 'v1',
      scannedAt: DateTime.utc(2026),
      card: const VisitorCard(
        userId: 'u1',
        name: 'Visitor One',
        nameArabic: 'Visitor One',
        available: true,
      ),
    );

Future<void> _openSheet(WidgetTester tester, _FakeExhibitorRepo repo) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        exhibitorRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: FilledButton(
                onPressed: () => unawaited(
                  showModalBottomSheet<bool>(
                    context: context,
                    builder: (_) => CapturedVisitorSheet(visitor: _visitor()),
                  ),
                ),
                child: const Text('open'),
              ),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

/// Pumps in small steps until the removal lands, then one more frame — which
/// leaves the tree part-way through the sheet's exit transition.
///
/// `pumpAndSettle` cannot be used here: once the exit finishes the sheet is
/// gone whether or not the flag was cleared, so it discriminates nothing. The
/// loop is needed because the confirm dialog's own exit has to finish before
/// `SimfConfirmDialog.show` resolves and the removal is even issued.
Future<void> _pumpToMidExit(WidgetTester tester, bool Function() done) async {
  for (var i = 0; i < 60 && !done(); i++) {
    await tester.pump(const Duration(milliseconds: 16));
  }
  expect(done(), isTrue, reason: 'the removal never reached the repository');
  await tester.pump(const Duration(milliseconds: 16));
}

ButtonStyleButton _removeButton(WidgetTester tester) =>
    tester.widget<FilledButton>(
      find.descendant(
        of: find.byType(CapturedVisitorSheet),
        matching: find.widgetWithText(FilledButton, _removeLabel),
      ),
    );

void main() {
  group('CapturedVisitorSheet busy flag', () {
    testWidgets('a confirmed removal leaves Remove disabled as the sheet exits',
        (tester) async {
      final repo = _FakeExhibitorRepo();
      await _openSheet(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _removeLabel));
      await tester.pumpAndSettle();
      expect(find.text('Remove this visitor?'), findsOneWidget);

      await tester.tap(find.text(_removeLabel).last);
      await _pumpToMidExit(tester, () => repo.removeCalls == 1);

      expect(find.byType(CapturedVisitorSheet), findsOneWidget);
      expect(
        _removeButton(tester).onPressed,
        isNull,
        reason: 'the sheet is leaving — re-enabling it repaints it on screen',
      );

      await tester.pumpAndSettle();
      expect(find.byType(CapturedVisitorSheet), findsNothing);
    });

    // The other half of the same rule: a sheet that is STAYING must get its
    // control back, which is what the `finally` was added for.
    testWidgets('a failed removal re-enables Remove on the sheet that stays',
        (tester) async {
      final repo = _FakeExhibitorRepo(removeStatus: 500);
      await _openSheet(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _removeLabel));
      await tester.pumpAndSettle();
      await tester.tap(find.text(_removeLabel).last);
      await tester.pumpAndSettle();

      expect(find.byType(CapturedVisitorSheet), findsOneWidget);
      expect(_removeButton(tester).onPressed, isNotNull);
    });
  });
}
