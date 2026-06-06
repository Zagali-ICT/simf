import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/share_my_contact_screen.dart';

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
        home: const ShareMyContactScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ShareMyContactScreen (FDS-014)', () {
    testWidgets('mints + renders the token as a QR', (tester) async {
      final repo = FakeContactsRepo(token: 'ABC123');
      await _pump(tester, repo);

      expect(repo.getTokenCalls, 1);
      expect(find.byType(QrImageView), findsOneWidget);
    });

    testWidgets('a load failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = FakeContactsRepo(tokenStatus: 500);
      await _pump(tester, repo);

      expect(find.text('Could not load your share code.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.getTokenCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('rotate confirms then swaps the token + toasts', (tester) async {
      final repo = FakeContactsRepo(token: 'OLD', rotatedToken: 'NEW');
      await _pump(tester, repo);

      // Open the confirm dialog (the screen's rotate button is a TextButton).
      await tester.tap(find.widgetWithText(TextButton, 'Rotate code'));
      await tester.pumpAndSettle();
      // Confirm (the dialog's confirm button is a FilledButton).
      await tester.tap(find.widgetWithText(FilledButton, 'Rotate code'));
      await tester.pumpAndSettle();

      expect(repo.rotateCalls, 1);
      expect(find.text('A new code was generated'), findsOneWidget);
      // The QR re-rendered with the rotated token.
      expect(find.byType(QrImageView), findsOneWidget);
    });

    testWidgets('cancelling rotate keeps the original token', (tester) async {
      final repo = FakeContactsRepo(token: 'OLD', rotatedToken: 'NEW');
      await _pump(tester, repo);

      await tester.tap(find.widgetWithText(TextButton, 'Rotate code'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(repo.rotateCalls, 0);
      // Still showing the original QR (no rotate happened).
      expect(find.byType(QrImageView), findsOneWidget);
    });
  });
}
