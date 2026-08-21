// Pins the busy flag on the handler that POPS the sheet: `Navigator.pop` does
// not dispose the State until the 200ms exit finishes, so a `mounted` guard
// still passes and clearing the flag flicks the spinner back in view. Hence the
// assertions mid-exit — after a `pumpAndSettle` both builds look the same.
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/widgets/contact_preview_sheet.dart';

import '../../support/simf_test_scope.dart';
import '_fake_contacts_repo.dart';

const _card = VisitorCard(
  userId: 'u1',
  name: 'Sara Ahmed',
  nameArabic: 'Sara',
  available: true,
  jobTitle: 'Captain',
  organisation: 'RSNF',
);

const _saveLabel = 'Save to My Contacts';

Future<void> _openPreview(WidgetTester tester, FakeContactsRepo repo) async {
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
                    isScrollControlled: true,
                    builder: (_) => const ContactPreviewSheet(
                      token: 'TOKEN-A',
                      card: _card,
                    ),
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

void main() {
  group('ContactPreviewSheet busy flag', () {
    testWidgets('a successful save keeps the spinner up while the sheet exits',
        (tester) async {
      final repo = FakeContactsRepo();
      await _openPreview(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _saveLabel));
      // The second pump lands mid-exit, where the flick-back would be visible.
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));

      expect(repo.saveCalls, 1);
      expect(find.byType(ContactPreviewSheet), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(ContactPreviewSheet),
          matching: find.byType(CircularProgressIndicator),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byType(ContactPreviewSheet),
          matching: find.byIcon(Icons.person_add_alt_1),
        ),
        findsNothing,
      );

      await tester.pumpAndSettle();
      expect(find.byType(ContactPreviewSheet), findsNothing);
    });

    // The other half: a sheet that STAYS must get its control back.
    testWidgets('a failed save re-enables the button on the sheet that stays',
        (tester) async {
      final repo = FakeContactsRepo(saveStatus: 400);
      await _openPreview(tester, repo);

      await tester.tap(find.widgetWithText(FilledButton, _saveLabel));
      await tester.pumpAndSettle();

      expect(find.byType(ContactPreviewSheet), findsOneWidget);
      expect(find.byIcon(Icons.person_add_alt_1), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(find.widgetWithText(FilledButton, _saveLabel))
            .onPressed,
        isNotNull,
      );
    });
  });
}
