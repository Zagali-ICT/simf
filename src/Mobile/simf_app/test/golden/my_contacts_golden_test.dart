@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/my_contacts_screen.dart';

import '../features/contacts/_fake_contacts_repo.dart';
import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Render-lock golden of the "My Contacts" screen (جهات الاتصال, FDS-014,
/// D-286). Regenerate:
///   flutter test --update-goldens test/golden/my_contacts_golden_test.dart
///
/// No KSA design frame is bound (interim UI, final visuals from SIMF-VID-001),
/// so this is a **structural** render-lock: the AppBar shell + scan action over
/// the saved-contact list (`SavedContactTile` rows — name / org·title subtitle /
/// chevron). RTL.

SavedContactRow _row(String id, String nameArabic, String job, String org) =>
    SavedContactRow(
      id: id,
      subjectUserId: 'u-$id',
      name: id,
      nameArabic: nameArabic,
      subjectAvailable: true,
      jobTitle: job,
      organisation: org,
    );

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My contacts @375x812 — saved-contact list (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final repo = FakeContactsRepo(
      saved: <SavedContactRow>[
        _row('c1', 'أحمد السالم', 'مدير العمليات', 'شركة الملاحة البحرية'),
        _row('c2', 'سارة نور', 'مهندسة', 'هيئة الموانئ'),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          contactsRepositoryProvider.overrideWithValue(repo),
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
          home: const MyContactsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(MyContactsScreen),
      matchesGoldenFile('goldens/my_contacts.png'),
    );
  });
}
