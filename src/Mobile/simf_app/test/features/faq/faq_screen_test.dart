import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/faq/data/faq_models.dart';
import 'package:simf_app/features/faq/data/faq_repository.dart';
import 'package:simf_app/features/faq/faq_screen.dart';

import '../../support/simf_test_scope.dart';

List<FaqGroup> _sample() => <FaqGroup>[
      const FaqGroup(
        id: 'g1',
        name: 'Registration',
        nameArabic: 'التسجيل',
        entries: <FaqEntry>[
          FaqEntry(
            id: 'e1',
            question: 'How do I register for the forum?',
            questionArabic: 'كيف أسجّل في الملتقى؟',
            answer: 'Register on the official website.',
            answerArabic: 'سجّل عبر الموقع الرسمي.',
          ),
          FaqEntry(
            id: 'e2',
            question: 'Is registration free?',
            questionArabic: 'هل التسجيل مجاني؟',
            answer: 'Yes, it is free.',
            answerArabic: 'نعم، التسجيل مجاني.',
          ),
        ],
      ),
    ];

Future<void> _pump(
  WidgetTester tester, {
  required Override faqOverride,
  Locale locale = const Locale('en'),
}) async {
  tester.view.physicalSize = const Size(375, 1600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[faqOverride],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const FaqScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('FaqScreen (Page 201 — KSA frame 1388:7567)', () {
    testWidgets('renders the questions collapsed, then expands on tap',
        (tester) async {
      await _pump(
        tester,
        faqOverride: faqProvider.overrideWith((ref) async => _sample()),
      );

      expect(find.text('FAQ'), findsOneWidget);
      // Both questions render; answers are hidden until tapped.
      expect(find.text('How do I register for the forum?'), findsOneWidget);
      expect(find.text('Is registration free?'), findsOneWidget);
      expect(find.text('Register on the official website.'), findsNothing);

      await tester.tap(find.text('How do I register for the forum?'));
      await tester.pumpAndSettle();
      expect(find.text('Register on the official website.'), findsOneWidget);
    });

    testWidgets('pull-to-refresh re-fetches the FAQ catalogue',
        (tester) async {
      var calls = 0;
      await _pump(
        tester,
        faqOverride: faqProvider.overrideWith((ref) async {
          calls++;
          return _sample();
        }),
      );
      expect(calls, 1); // initial load

      // Pull down on the always-scrollable list to fire the RefreshIndicator.
      await tester.fling(
        find.text('How do I register for the forum?'),
        const Offset(0, 400),
        1000,
      );
      await tester.pumpAndSettle();

      expect(calls, 2); // refreshed → the provider re-ran
    });

    testWidgets('shows the empty state when there are no entries',
        (tester) async {
      await _pump(
        tester,
        faqOverride:
            faqProvider.overrideWith((ref) async => const <FaqGroup>[]),
      );
      expect(
        find.text('No frequently asked questions yet.'),
        findsOneWidget,
      );
    });

    testWidgets('shows the error state with retry on a wire failure',
        (tester) async {
      await _pump(
        tester,
        faqOverride: faqProvider
            .overrideWith((ref) => Future<List<FaqGroup>>.error('boom')),
      );
      expect(find.text('Could not load the FAQ.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });
  });
}
