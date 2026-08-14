import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/session_header_card.dart';

SessionDetail _detail({String? categoryName, String? categoryNameArabic}) =>
    SessionDetail(
      id: 's1',
      code: 'OP-1',
      displayOrder: 2,
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: DateTime.utc(2026, 11, 23, 6),
      end: DateTime.utc(2026, 11, 23, 7, 30),
      speakers: const <SessionSpeaker>[],
      categoryName: categoryName,
      categoryNameArabic: categoryNameArabic,
    );

Future<void> _pumpCard(
  WidgetTester tester, {
  required SessionDetail detail,
  bool titleAndTimeOnly = false,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => SessionHeaderCard(
            detail: detail,
            isArabic: AppL10n.of(context).isArabic,
            l10n: AppL10n.of(context),
            onSessionLink: () {},
            onSessionSummary: () {},
            summaryEnabled: false,
            liveEnabled: false,
            titleAndTimeOnly: titleAndTimeOnly,
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  // PAR-D3 — the session-detail header card carries the category tag pill
  // again.
  group('SessionHeaderCard category pill (PAR-D3)', () {
    testWidgets('a session WITH a category renders the pill under the title',
        (tester) async {
      await _pumpCard(tester, detail: _detail(categoryName: 'Main Session'));

      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Main Session'), findsOneWidget);
    });

    testWidgets('a session with NO category renders no pill', (tester) async {
      await _pumpCard(tester, detail: _detail());

      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Main Session'), findsNothing);
    });

    testWidgets('a blank category is treated as none', (tester) async {
      await _pumpCard(tester, detail: _detail(categoryName: '   '));

      expect(find.text('   '), findsNothing);
    });

    // #29 — the workshop reduction drops the pill AND the two action buttons.
    testWidgets('titleAndTimeOnly drops the pill and the action row',
        (tester) async {
      await _pumpCard(
        tester,
        detail: _detail(categoryName: 'Main Session'),
        titleAndTimeOnly: true,
      );

      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Main Session'), findsNothing);
      expect(find.text('Session summary'), findsNothing);
      expect(find.text('Session link'), findsNothing);
    });

    testWidgets('the full card keeps both action buttons', (tester) async {
      await _pumpCard(tester, detail: _detail(categoryName: 'Main Session'));

      expect(find.text('Session summary'), findsOneWidget);
      expect(find.text('Session link'), findsOneWidget);
    });
  });
}
