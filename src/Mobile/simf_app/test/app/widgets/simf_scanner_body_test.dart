import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_scanner_body.dart';

Future<void> _pump(
  WidgetTester tester, {
  required Future<void> Function(String) onCode,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('en'),
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: SingleChildScrollView(
          child: SimfScannerBody(
            fieldLabel: 'Code',
            continueLabel: 'Look up',
            // Camera off so the manual path drives the test (no native plugin).
            enableCamera: false,
            onCode: onCode,
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SimfScannerBody (D-737)', () {
    testWidgets('manual submit calls onCode once with the trimmed value',
        (tester) async {
      final codes = <String>[];
      await _pump(tester, onCode: (c) async => codes.add(c));

      await tester.enterText(find.byType(TextField), '  ABC123  ');
      await tester.tap(find.widgetWithText(FilledButton, 'Look up'));
      await tester.pumpAndSettle();

      expect(codes, <String>['ABC123']);
    });

    testWidgets('camera off → no "or" divider, manual entry is the only path',
        (tester) async {
      await _pump(tester, onCode: (_) async {});
      // The gold "or" divider only appears with the camera section.
      expect(find.text('or'), findsNothing);
      expect(find.byType(TextField), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Look up'), findsOneWidget);
    });

    testWidgets('the submit button is disabled while onCode is pending',
        (tester) async {
      final gate = Completer<void>();
      await _pump(tester, onCode: (_) => gate.future);

      await tester.enterText(find.byType(TextField), 'ABC');
      await tester.tap(find.widgetWithText(FilledButton, 'Look up'));
      await tester.pump(); // let setState(_processing = true) apply

      final button = tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Look up'));
      expect(button.onPressed, isNull); // disabled

      gate.complete();
      await tester.pumpAndSettle();
      final after = tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Look up'));
      expect(after.onPressed, isNotNull); // re-enabled
    });
  });
}
