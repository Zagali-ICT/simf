@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/delegations_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the Delegations screen against Figma frame **1426:10771**
/// (الوفود). Compare to the frame:
///   flutter test --update-goldens test/golden/delegations_golden_test.dart
///
/// Proves the stats strip (8 دولة مشاركة left / 54 إجمالي المشاركين right + the
/// scattered flags + faint gold grid), the search field (search glyph at the
/// inline-start = right, the filter cell walled off at the inline-end = left),
/// and the per-country card: the flag box + bilingual country name on the right,
/// the head-of-delegation box (gold avatar + name on the right, رئيس الوفد chip
/// on the left), and the bottom row — the member chip on the **right** with the
/// groups glyph leading it, the date range on the **left** with the clock glyph
/// leading it (Figma 1426:10855/10862/10856). RTL throughout.
///
/// Fixed data only (no DateTime.now() on the display path), so the PNG is stable
/// run-to-run. Known golden-env artifacts (NOT layout defects): the country
/// flags are colour-emoji glyphs → render as tofu (no colour-emoji font loaded);
/// the date range derives from fixed dates so it is deterministic.

final _delegations = Delegations(
  countryCount: 8,
  totalParticipants: 54,
  items: <DelegationItem>[
    DelegationItem(
      countryId: 682,
      countryCode: 'SA',
      countryName: 'Saudi Arabia',
      countryNameArabic: 'المملكة العربية السعودية',
      memberCount: 12,
      headName: 'Prince Abdulaziz',
      headNameArabic: 'سمو الأمير عبدالعزيز',
      headTitle: 'نائب وزير الدفاع',
      arrivalDate: DateTime(2026, 1, 12),
      departureDate: DateTime(2026, 1, 15),
    ),
    DelegationItem(
      countryId: 840,
      countryCode: 'US',
      countryName: 'United States',
      countryNameArabic: 'الولايات المتحدة',
      memberCount: 8,
      headName: 'James Mitchell',
      headNameArabic: 'James Mitchell',
      headTitle: 'السفير الأمريكي لدى المملكة',
      arrivalDate: DateTime(2026, 1, 12),
      departureDate: DateTime(2026, 1, 15),
    ),
    DelegationItem(
      countryId: 784,
      countryCode: 'AE',
      countryName: 'UAE',
      countryNameArabic: 'الإمارات العربية المتحدة',
      memberCount: 7,
      headName: 'Maj. Gen. Al-Shamsi',
      headNameArabic: 'اللواء محمد الشامسي',
      headTitle: 'نائب رئيس الأركان',
      arrivalDate: DateTime(2026, 1, 12),
      departureDate: DateTime(2026, 1, 15),
    ),
  ],
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Delegations @375x1075 — Figma 1426:10771 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1075);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          // The public screen reads the meeting-access flags for card
          // tappability; a guest (none) keeps the plain info cards (matches the
          // delegations widget-test harness).
          currentUserMeetingAccessProvider
              .overrideWith((ref) => MeetingAccess.none),
          delegationsProvider.overrideWith((ref) async => _delegations),
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
          home: const DelegationsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(DelegationsScreen),
      matchesGoldenFile('goldens/delegations_1426-10771.png'),
    );
  });
}
