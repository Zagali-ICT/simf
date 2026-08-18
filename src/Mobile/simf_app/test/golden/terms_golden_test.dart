@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_app/features/content/terms_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Terms screen against Figma frame **505:1553**
/// (الشروط والأحكام). Regenerate:
///   flutter test --update-goldens test/golden/terms_golden_test.dart
///
/// Frame parity expected: the navy surface + the top-right diagonal sweep, the
/// centred title with a left chevron, the "معلومات هامة لزوار الملتقى" heading,
/// each server body line as a gold-hairline bullet card, and the full-width
/// gold موافق button pinned at the bottom. RTL.
///
/// A pinned content block (three body lines) → the PNG is stable run-to-run.

class _FakeContentRepository implements ContentRepository {
  @override
  Future<ContentBlock> getContentBlock(String key) async => const ContentBlock(
        key: 'terms',
        content: 'First important note for forum visitors.\n'
            'Second note about the venue and badges.\n'
            'Third note about session times.',
        contentArabic: 'المعلومة الأولى المهمة لزوار الملتقى.\n'
            'المعلومة الثانية حول المكان والبطاقات.\n'
            'المعلومة الثالثة حول مواعيد الجلسات.',
      );
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Terms @375x900 — Figma 505:1553 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          contentRepositoryProvider.overrideWithValue(_FakeContentRepository()),
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
          home: const TermsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(TermsScreen),
      matchesGoldenFile('goldens/terms_505-1553.png'),
    );
  });
}
