import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/scan_contact_screen.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';

import '_fake_contacts_repo.dart';

Future<void> _pump(WidgetTester tester, FakeContactsRepo repo) async {
  await tester.pumpWidget(
    ProviderScope(
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
        // Camera off — the manual-entry path is what the tests drive.
        home: const ScanContactScreen(enableCamera: false),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _lookUp(WidgetTester tester, String code) async {
  await tester.enterText(find.byType(TextField).first, code);
  await tester.tap(find.widgetWithText(FilledButton, 'Look up'));
  await tester.pumpAndSettle();
}

void main() {
  group('ScanContactScreen (FDS-014)', () {
    testWidgets('manual look-up resolves + previews the card', (tester) async {
      final repo = FakeContactsRepo();
      await _pump(tester, repo);

      await _lookUp(tester, 'TOKEN-A');

      expect(repo.resolveCalls, 1);
      expect(repo.lastResolvedToken, 'TOKEN-A');
      expect(find.byType(ContactCard), findsOneWidget);
      expect(find.text('Sara Ahmed'), findsOneWidget);
    });

    testWidgets('saving from the preview calls save + toasts', (tester) async {
      final repo = FakeContactsRepo();
      await _pump(tester, repo);

      await _lookUp(tester, 'TOKEN-A');
      await tester.enterText(find.byType(TextField).last, 'met at booth 3');
      await tester.tap(find.widgetWithText(FilledButton, 'Save to My Contacts'));
      await tester.pumpAndSettle();

      expect(repo.saveCalls, 1);
      expect(repo.lastSavedToken, 'TOKEN-A');
      expect(repo.lastSavedNote, 'met at booth 3');
      expect(find.text('Contact saved'), findsOneWidget);
    });

    testWidgets('an unknown token (404) shows the not-found toast',
        (tester) async {
      final repo = FakeContactsRepo(resolveStatus: 404);
      await _pump(tester, repo);

      await _lookUp(tester, 'BAD');

      expect(find.text('Code not found or no longer valid.'), findsOneWidget);
      expect(find.byType(ContactCard), findsNothing);
    });

    testWidgets('saving your own token (400) shows the self-save toast',
        (tester) async {
      final repo = FakeContactsRepo(saveStatus: 400);
      await _pump(tester, repo);

      await _lookUp(tester, 'MINE');
      await tester.tap(find.widgetWithText(FilledButton, 'Save to My Contacts'));
      await tester.pumpAndSettle();

      expect(find.text('You can’t save your own card.'), findsOneWidget);
    });

    testWidgets('an unavailable subject hides the save action', (tester) async {
      final repo = FakeContactsRepo(
        card: const VisitorCard(
          userId: 'u9',
          name: '',
          nameArabic: '',
          available: false,
        ),
      );
      await _pump(tester, repo);

      await _lookUp(tester, 'GONE');

      expect(find.text('This contact is no longer available'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Save to My Contacts'),
        findsNothing,
      );
    });
  });
}
