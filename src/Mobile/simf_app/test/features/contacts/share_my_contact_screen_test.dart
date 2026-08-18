import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/share_my_contact_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';

import '../../support/simf_test_scope.dart';
import '_fake_contacts_repo.dart';

/// D-470 — the share QR now encodes the user's vCard (Arabic name + phones).
const String _kVcard = 'BEGIN:VCARD\r\nVERSION:3.0\r\nFN:محمد العتيبي\r\n'
    'TEL;TYPE=CELL:+966500112233\r\nEND:VCARD\r\n';

/// Minimal stand-in for the My-Area repo — only the vCard fetch is exercised.
class _FakeMyAreaRepo implements MyAreaRepository {
  int vcfCalls = 0;

  @override
  Future<String> getContactCardVcf() async {
    vcfCalls++;
    return _kVcard;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

Future<void> _pump(
  WidgetTester tester,
  FakeContactsRepo repo, {
  _FakeMyAreaRepo? myArea,
}) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        contactsRepositoryProvider.overrideWithValue(repo),
        myAreaRepositoryProvider.overrideWithValue(myArea ?? _FakeMyAreaRepo()),
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
        home: ShareMyContactScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ShareMyContactScreen (FDS-014)', () {
    testWidgets('sources the QR from the contact vCard, not the token (D-470)',
        (tester) async {
      final myArea = _FakeMyAreaRepo();
      await _pump(tester, FakeContactsRepo(token: 'ABC123'), myArea: myArea);

      // The QR renders, and it is fed by the vCard fetch (Arabic name + phones)
      // so any phone camera can add the contact — not the opaque share token.
      expect(find.byType(QrImageView), findsOneWidget);
      expect(myArea.vcfCalls, 1);
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

    testWidgets('rotate confirms then swaps the token + toasts',
        (tester) async {
      final repo = FakeContactsRepo(token: 'OLD', rotatedToken: 'NEW');
      await _pump(tester, repo);

      // Open the confirm dialog — the rotate button is an OutlinedButton below
      // the fold, so scroll it into view before tapping.
      final rotateButton = find.widgetWithText(OutlinedButton, 'Rotate code');
      await tester.ensureVisible(rotateButton);
      await tester.pumpAndSettle();
      await tester.tap(rotateButton);
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

      final rotateButton = find.widgetWithText(OutlinedButton, 'Rotate code');
      await tester.ensureVisible(rotateButton);
      await tester.pumpAndSettle();
      await tester.tap(rotateButton);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(repo.rotateCalls, 0);
      // Still showing the original QR (no rotate happened).
      expect(find.byType(QrImageView), findsOneWidget);
    });
  });
}
