import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/forum_guide/forum_guide_screen.dart';

Future<void> _pump(
  WidgetTester tester, {
  Locale locale = const Locale('en'),
}) async {
  // A tall surface so the lazy ListView lays out the banner + all five step
  // cards (frame 1388:7493).
  tester.view.physicalSize = const Size(375, 1600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    ProviderScope(
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const ForumGuideScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ForumGuideScreen (Page 200 — KSA frame 1388:7493)', () {
    testWidgets('renders the title, intro banner and five numbered steps',
        (tester) async {
      await _pump(tester);

      // Header title.
      expect(find.text('Forum guide'), findsOneWidget);
      // Gold intro banner copy.
      expect(find.textContaining('Welcome to SIMF 2026'), findsOneWidget);

      // The five gold index badges 1..5.
      for (final n in <String>['1', '2', '3', '4', '5']) {
        expect(find.text(n), findsOneWidget);
      }

      // Distinct step titles.
      expect(find.text('Explore the sessions'), findsOneWidget);
      expect(find.text('On-site registration & entry'), findsOneWidget);
      expect(find.text('Reach the speakers'), findsOneWidget);
      // Steps 1 and 5 share the design's (placeholder) title.
      expect(find.text('Registration & sign-in'), findsNWidgets(2));
    });

    testWidgets('Arabic: the step number sits to the right of its title',
        (tester) async {
      await _pump(tester, locale: const Locale('ar'));

      final numberX = tester.getCenter(find.text('2')).dx;
      final titleX = tester.getCenter(find.text('استكشاف الجلسات')).dx;
      // RTL — the gold badge is at the inline start (physical right).
      expect(numberX, greaterThan(titleX));
    });
  });
}
