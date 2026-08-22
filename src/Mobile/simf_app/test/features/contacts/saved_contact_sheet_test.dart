// Pins the busy flag on the handler that POPS the sheet: `mounted` stays true
// through the ~200ms exit transition after `pop()`.
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/widgets/saved_contact_sheet.dart';

import '../../support/simf_test_scope.dart';
import '_fake_contacts_repo.dart';

const _row = SavedContactRow(
  id: 's1',
  subjectUserId: 'u1',
  name: 'Sara Ahmed',
  nameArabic: 'Sara',
  subjectAvailable: true,
  jobTitle: 'Captain',
  organisation: 'RSNF',
);

const _removeLabel = 'Remove';

Future<void> _openSheet(WidgetTester tester, FakeContactsRepo repo) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        contactsRepositoryProvider.overrideWithValue(repo),
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
                    builder: (_) => const SavedContactSheet(row: _row),
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

/// Pumps until the removal lands, then one more frame, leaving the tree
/// part-way through the sheet's exit. `pumpAndSettle` discriminates nothing
/// here: once the exit finishes the sheet is gone either way.
Future<void> _pumpToMidExit(WidgetTester tester, bool Function() done) async {
  for (var i = 0; i < 60 && !done(); i++) {
    await tester.pump(const Duration(milliseconds: 16));
  }
  expect(done(), isTrue, reason: 'the removal never reached the repository');
  await tester.pump(const Duration(milliseconds: 16));
}

void main() {
  group('SavedContactSheet busy flag', () {
    testWidgets('a confirmed removal leaves Remove disabled as the sheet exits',
        (tester) async {
      final repo = FakeContactsRepo();
      await _openSheet(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _removeLabel));
      await tester.pumpAndSettle();
      expect(find.text('Remove contact?'), findsOneWidget);

      await tester.tap(find.text(_removeLabel).last);
      await _pumpToMidExit(tester, () => repo.removeCalls == 1);

      expect(find.byType(SavedContactSheet), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.descendant(
                of: find.byType(SavedContactSheet),
                matching: find.widgetWithText(FilledButton, _removeLabel),
              ),
            )
            .onPressed,
        isNull,
        reason: 'the sheet is leaving — re-enabling it repaints it on screen',
      );

      await tester.pumpAndSettle();
      expect(find.byType(SavedContactSheet), findsNothing);
    });

    // The other half: a sheet that is STAYING must get its control back.
    testWidgets('a failed removal re-enables Remove on the sheet that stays',
        (tester) async {
      final repo = FakeContactsRepo(removeStatus: 500);
      await _openSheet(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _removeLabel));
      await tester.pumpAndSettle();
      await tester.tap(find.text(_removeLabel).last);
      await tester.pumpAndSettle();

      expect(find.byType(SavedContactSheet), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.descendant(
                of: find.byType(SavedContactSheet),
                matching: find.widgetWithText(FilledButton, _removeLabel),
              ),
            )
            .onPressed,
        isNotNull,
      );
    });
  });
}
