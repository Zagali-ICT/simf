import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/my_contacts_screen.dart';

import '../../support/simf_test_scope.dart';
import '_fake_contacts_repo.dart';

SavedContactRow _row({
  String id = 's1',
  String name = 'Sara Ahmed',
  bool available = true,
}) =>
    SavedContactRow(
      id: id,
      subjectUserId: 'u1',
      name: name,
      nameArabic: 'سارة',
      subjectAvailable: available,
      jobTitle: 'Captain',
      organisation: 'RSNF',
      note: 'met at booth 3',
    );

Future<void> _pump(WidgetTester tester, FakeContactsRepo repo) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        contactsRepositoryProvider.overrideWithValue(repo),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: MyContactsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MyContactsScreen (FDS-014)', () {
    testWidgets('renders the saved-contact rows', (tester) async {
      final repo = FakeContactsRepo(
        saved: <SavedContactRow>[_row(), _row(id: 's2', name: 'Omar Ali')],
      );
      await _pump(tester, repo);

      expect(repo.listCalls, 1);
      expect(find.text('Sara Ahmed'), findsOneWidget);
      expect(find.text('Omar Ali'), findsOneWidget);
    });

    testWidgets('an empty list shows the empty state', (tester) async {
      await _pump(tester, FakeContactsRepo(saved: <SavedContactRow>[]));
      expect(find.text('No saved contacts yet'), findsOneWidget);
    });

    testWidgets('a load failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = FakeContactsRepo(listStatus: 500);
      await _pump(tester, repo);

      expect(find.text('Could not load your contacts.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.listCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('opening a row then removing deletes + reloads',
        (tester) async {
      final repo = FakeContactsRepo(saved: <SavedContactRow>[_row()]);
      await _pump(tester, repo);

      // Open the detail sheet.
      await tester.tap(find.text('Sara Ahmed'));
      await tester.pumpAndSettle();

      // Tap the sheet's Remove button.
      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      // Confirm in the dialog.
      await tester.tap(
        find.descendant(
          of: find.byType(Dialog),
          matching: find.widgetWithText(FilledButton, 'Remove'),
        ),
      );
      await tester.pumpAndSettle();

      expect(repo.removeCalls, 1);
      expect(repo.lastRemovedId, 's1');
      expect(find.text('Removed'), findsOneWidget);
      // The list reloaded and is now empty.
      expect(find.text('No saved contacts yet'), findsOneWidget);
    });
  });
}
