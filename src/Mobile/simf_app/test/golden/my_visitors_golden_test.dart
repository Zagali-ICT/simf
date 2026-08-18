@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/my_visitors_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Render-lock golden of the exhibitor "My Booth Visitors" screen (زوار جناحي,
/// D-426). Regenerate: flutter test --update-goldens
/// test/golden/my_visitors_golden_test.dart
///
/// No Figma frame is bound for this screen (it is a D-426 functional page, not
/// a KSA design frame), so this is a **structural** render-lock: the navy shell
/// over the BUG-025 "not My Contacts" note and the captured-visitor list, each
/// a shared `ContactCard`. RTL.

class _StubExhibitorRepo implements ExhibitorRepository {
  _StubExhibitorRepo(this._visitors);

  final List<ExhibitorVisitor> _visitors;

  @override
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) async =>
      throw UnimplementedError();

  @override
  Future<List<ExhibitorVisitor>> listMyVisitors() async => _visitors;

  // FR-EXH-002 — the per-lead actions live behind a tap, so the render-lock
  // golden never reaches either.
  @override
  Future<void> removeVisitor(String id) async => throw UnimplementedError();

  @override
  Future<String> getVcard(String id) async => throw UnimplementedError();
}

ExhibitorVisitor _visitor(String id, VisitorCard card) =>
    ExhibitorVisitor(id: id, scannedAt: DateTime.utc(2026), card: card);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My visitors @375x812 — captured-visitor list (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final repo = _StubExhibitorRepo(<ExhibitorVisitor>[
      _visitor(
        'v-1',
        const VisitorCard(
          userId: 'u-1',
          name: 'Ahmed Al-Salem',
          nameArabic: 'أحمد السالم',
          available: true,
          jobTitle: 'مدير العمليات',
          organisation: 'Maritime Co',
          organisationArabic: 'شركة الملاحة البحرية',
          email: 'ahmed@example.sa',
          saudiMobile: '0500000000',
          countryName: 'Saudi Arabia',
          countryNameArabic: 'السعودية',
        ),
      ),
      _visitor(
        'v-2',
        const VisitorCard(
          userId: 'u-2',
          name: 'Sara Noor',
          nameArabic: 'سارة نور',
          available: false,
          jobTitle: 'مهندسة',
          organisation: 'Port Authority',
          organisationArabic: 'هيئة الموانئ',
          email: 'sara@example.sa',
          internationalMobile: '+201000000000',
          countryName: 'Egypt',
          countryNameArabic: 'مصر',
        ),
      ),
    ]);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          exhibitorRepositoryProvider.overrideWithValue(repo),
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
          home: const MyVisitorsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(MyVisitorsScreen),
      matchesGoldenFile('goldens/my_visitors.png'),
    );
  });
}
