@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/data/staff_repository.dart';
import 'package:simf_app/features/staff/register_visitor_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the staff walk-in registration (إنشاء ملف زائر · iPad Pro
/// 12.9" 1024×1314). Regenerate:
///   flutter test --update-goldens test/golden/staff_register_visitor_golden_test.dart
///
/// **Re-locked after the BUG-019 design-system rebuild (2026-07-26).** The
/// screen now renders the shared account chrome (`SimfFormScaffold`: back
/// chevron + the shared EN/ع language pill + the crest header) over the beige
/// card, with the two-column field grid built from the shared field widgets
/// (`simfFieldDecoration` inputs, `SimfPickerField` lookups, the gender pills,
/// the beige document tabs, the attachment fields and the terms + gold التالي
/// CTA). Locks the layout, typography, colour, spacing and RTL of the rebuilt
/// tablet screen — the previous golden captured the hand-rolled header, the
/// filled fields and the raw Material dropdowns the owner reported.
class _FakeProfileRepo implements ProfileRepository {
  @override
  Future<List<CountryItem>> getCountries() async => const <CountryItem>[
        CountryItem(code: 'SA', name: 'Saudi Arabia', nameArabic: 'السعودية'),
        CountryItem(code: 'EG', name: 'Egypt', nameArabic: 'مصر'),
      ];

  @override
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) async =>
      const <ProfileTypeItem>[
        ProfileTypeItem(
          id: 'pt-normal',
          name: 'Normal',
          nameArabic: 'عادي',
          isVisitor: true,
        ),
      ];

  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) async =>
      const <OrganisationItem>[
        OrganisationItem(id: 'org-1', nameAr: 'أكمي', nameEn: 'Acme'),
      ];

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

class _FakeStaffRepo implements StaffRepository {
  @override
  Future<StaffWalkInResult> registerVisitor(StaffWalkInRequest request) async =>
      const StaffWalkInResult(
        userId: 'u1',
        displayName: '',
        qrId: '',
        profileTypeName: 'Normal',
      );

  @override
  Future<bool> uploadIdImage({
    required String userId,
    required List<int> bytes,
    required String filename,
  }) async =>
      true;

  @override
  Future<bool> uploadAvatar({
    required String userId,
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Staff register-visitor @1024×1314 — Figma 1467:12357 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(1024, 1314);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          profileRepositoryProvider.overrideWithValue(_FakeProfileRepo()),
          staffRepositoryProvider.overrideWithValue(_FakeStaffRepo()),
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
          home: const StaffRegisterVisitorScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(StaffRegisterVisitorScreen),
      matchesGoldenFile('goldens/staff_register_visitor_1467-12357.png'),
    );
  });
}
