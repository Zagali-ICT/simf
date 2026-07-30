import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_scanner_frame.dart';

Future<void> _pumpAt(WidgetTester tester, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.reset);

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
      home: const Scaffold(
        body: Center(child: SimfScannerFrame(statusLabel: 'Scanning')),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SimfScannerFrame — BUG-019 / 19e responsive viewfinder', () {
    testWidgets('a phone keeps the Figma 343 card width', (tester) async {
      await _pumpAt(tester, const Size(400, 900));
      expect(tester.getSize(find.byType(SimfScannerFrame)).width, 343);
    });

    testWidgets('a tablet panel scales the card up instead of leaving a '
        'phone-sized viewfinder', (tester) async {
      await _pumpAt(tester, const Size(1600, 1000));
      final width = tester.getSize(find.byType(SimfScannerFrame)).width;
      expect(width, greaterThan(343));
      // Clamped, not stretched edge-to-edge.
      expect(width, lessThan(1600));
    });

    testWidgets('a very narrow window never overflows the screen',
        (tester) async {
      await _pumpAt(tester, const Size(320, 800));
      expect(tester.getSize(find.byType(SimfScannerFrame)).width, 320);
      expect(tester.takeException(), isNull);
    });
  });
}
