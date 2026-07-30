import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/feedback/widgets/star_row.dart';

/// Regression cover for **BUG-012** — the /rate screen exposed seven unnamed
/// tappables: the star controls were bare glyphs inside a [GestureDetector], so
/// a screen-reader user could not tell the stars apart and could not submit a
/// rating at all.
Future<void> _pump(
  WidgetTester tester, {
  required int value,
  required ValueChanged<int> onChanged,
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Center(child: StarRow(value: value, onChanged: onChanged)),
      ),
    ),
  );
  // The l10n delegate resolves asynchronously, so the first frame is empty.
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('every star is a named button', (tester) async {
    final handle = tester.ensureSemantics();

    await _pump(tester, value: 0, onChanged: (_) {});

    for (final label in <String>[
      '1 star',
      '2 stars',
      '3 stars',
      '4 stars',
      '5 stars',
    ]) {
      expect(find.bySemanticsLabel(label), findsOneWidget, reason: label);
    }

    handle.dispose();
  });

  testWidgets('the current score is reported as the selected stars',
      (tester) async {
    final handle = tester.ensureSemantics();

    await _pump(tester, value: 3, onChanged: (_) {});

    expect(
      tester.getSemantics(find.bySemanticsLabel('3 stars')),
      isSemantics(label: '3 stars', isButton: true, isSelected: true),
    );
    expect(
      tester.getSemantics(find.bySemanticsLabel('4 stars')),
      isSemantics(label: '4 stars', isButton: true, isSelected: false),
    );

    handle.dispose();
  });

  testWidgets('tapping a named star reports that score', (tester) async {
    final handle = tester.ensureSemantics();
    int? picked;

    await _pump(tester, value: 0, onChanged: (v) => picked = v);
    await tester.tap(find.bySemanticsLabel('4 stars'));

    expect(picked, 4);

    handle.dispose();
  });

  testWidgets('the labels are Arabic under the Arabic locale', (tester) async {
    final handle = tester.ensureSemantics();

    await _pump(
      tester,
      value: 0,
      onChanged: (_) {},
      locale: const Locale('ar'),
    );

    expect(find.bySemanticsLabel('نجمة واحدة'), findsOneWidget);
    expect(find.bySemanticsLabel('نجمتان'), findsOneWidget);
    expect(find.bySemanticsLabel('5 نجوم'), findsOneWidget);

    handle.dispose();
  });
}
