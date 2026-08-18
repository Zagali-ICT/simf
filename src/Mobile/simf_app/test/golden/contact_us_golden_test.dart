@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/contact_us/contact_us_screen.dart';
import 'package:simf_app/features/contact_us/data/contact_us_repository.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Contact-us screen against Figma frame **1388:7711**
/// (تواصل معنا). Regenerate: flutter test --update-goldens
/// test/golden/contact_us_golden_test.dart
///
/// Frame parity expected: the navy shell over the "أرسل رسالة" form card (name
/// / email / message fields + gold send button), the "معلومات التواصل" panel
/// (phone / email / location rows, each with a gold icon box + a beige
/// divider), and the "وسائل التواصل الاجتماعي" row of bordered brand boxes.
/// RTL.
///
/// A pinned org profile (all fields set) + a no-op repo → the PNG is stable
/// run-to-run.

class _NoopRepo implements ContactUsRepository {
  @override
  Future<void> submit({
    required String name,
    required String email,
    required String message,
  }) async {}
}

class _StubOrgProfile extends OrgProfileController {
  _StubOrgProfile(this._value);
  final OrgProfile? _value;
  @override
  OrgProfile? build() => _value;
  @override
  Future<void> warm() async {}
}

const _profile = OrgProfile(
  name: 'SIMF',
  nameArabic: 'سيمف',
  title: 'Forum',
  titleArabic: 'الملتقى',
  currentYear: 2026,
  status: 'Open',
  social: OrgSocial(
    x: 'https://x.com/simf',
    instagram: 'https://instagram.com/simf',
    linkedin: 'https://linkedin.com/company/simf',
    youtube: 'https://youtube.com/@simf',
    tiktok: 'https://tiktok.com/@simf',
  ),
  aboutItems: <OrgAboutItem>[],
  details: <OrgDetail>[],
  contactPhone: '+966 11 123 4567',
  contactEmail: 'info@simf2026.sa',
  locationText: 'Riyadh Convention Center',
  locationTextArabic: 'مركز الرياض للمؤتمرات',
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Contact us @375x1200 — Figma 1388:7711 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          contactUsRepositoryProvider.overrideWithValue(_NoopRepo()),
          orgProfileProvider.overrideWith(() => _StubOrgProfile(_profile)),
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
          home: const ContactUsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(ContactUsScreen),
      matchesGoldenFile('goldens/contact_us_1388-7711.png'),
    );
  });
}
