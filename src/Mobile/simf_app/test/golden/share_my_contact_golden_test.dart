@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/share_my_contact_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';

import '../features/contacts/_fake_contacts_repo.dart';
import 'golden_fonts.dart';

/// Render-lock golden of the "Share my contact" screen against the
/// owner-supplied frame **1701:6062** (مشاركة جهة اتصالي, FDS-014). Regenerate:
/// flutter test --update-goldens test/golden/share_my_contact_golden_test.dart
///
/// Frame parity expected: the AppBar shell over the centred QR card (the vCard
/// QR on a light surface card), the scan hint, the share (.vcf) filled action,
/// and the rotate-code text action. RTL.

const String _kVcard = 'BEGIN:VCARD\r\nVERSION:3.0\r\nFN:محمد العتيبي\r\n'
    'TEL;TYPE=CELL:+966500112233\r\nEND:VCARD\r\n';

class _FakeMyAreaRepo implements MyAreaRepository {
  @override
  Future<String> getContactCardVcf() async => _kVcard;

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Share my contact @375x812 — Figma 1701:6062 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          contactsRepositoryProvider
              .overrideWithValue(FakeContactsRepo(token: 'SHARE-TOKEN')),
          myAreaRepositoryProvider.overrideWithValue(_FakeMyAreaRepo()),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          locale: const Locale('ar'),
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

    await expectLater(
      find.byType(ShareMyContactScreen),
      matchesGoldenFile('goldens/share_my_contact_1701-6062.png'),
    );
  });
}
