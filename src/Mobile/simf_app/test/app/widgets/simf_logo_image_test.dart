import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_image_viewer.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';

/// Regression cover for the owner's 2026-07-26 logo request — "make the logo
/// fit its box in all logo views, and on-press show it in full size". The
/// shared [SimfLogoImage] is the single place both rules live, so they are
/// asserted here once rather than per page.
///
/// Network bytes never load under the test binding, so these tests exercise the
/// widget's contract (fit, fallback chain, tap target) rather than a decoded
/// picture — the tap wrapper sits outside the loading/error state, so it is
/// reachable either way.
Future<void> _pump(WidgetTester tester, Widget child) async {
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
        body: Center(child: SizedBox(width: 96, height: 96, child: child)),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  group('SimfLogoImage (owner 2026-07-26 — logo fit + tap to full size)', () {
    testWidgets('defaults to BoxFit.contain so a logo FITS its box',
        (tester) async {
      await _pump(
        tester,
        const SimfLogoImage(
          url: 'https://example.test/logo.png',
          placeholder: Text('LOADING'),
          semanticLabel: 'SAMI',
        ),
      );

      final image = tester.widget<Image>(find.byType(Image));
      expect(image.fit, BoxFit.contain);
    });

    testWidgets('a photographic caller can still ask for BoxFit.cover',
        (tester) async {
      await _pump(
        tester,
        const SimfLogoImage(
          url: 'https://example.test/photo.png',
          placeholder: Text('LOADING'),
          semanticLabel: 'Raed',
          fit: BoxFit.cover,
        ),
      );

      expect(tester.widget<Image>(find.byType(Image)).fit, BoxFit.cover);
    });

    testWidgets('a blank url renders onError without fetching', (tester) async {
      await _pump(
        tester,
        const SimfLogoImage(
          url: '   ',
          placeholder: Text('LOADING'),
          semanticLabel: 'SAMI',
          onError: _initials,
        ),
      );

      expect(find.text('SAMI-INITIALS'), findsOneWidget);
      expect(find.byType(Image), findsNothing);
    });

    testWidgets('tapping opens the full-size viewer', (tester) async {
      await _pump(
        tester,
        const SimfLogoImage(
          url: 'https://example.test/logo.png',
          placeholder: Text('LOADING'),
          semanticLabel: 'SAMI',
        ),
      );

      expect(find.byType(SimfImageViewer), findsNothing);
      await tester.tap(find.byType(SimfLogoImage));
      await tester.pumpAndSettle();

      expect(find.byType(SimfImageViewer), findsOneWidget);
      // The viewer is dismissible and names the picture for a screen reader.
      expect(find.byType(InteractiveViewer), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey<String>('imageViewerClose')));
      await tester.pumpAndSettle();
      expect(find.byType(SimfImageViewer), findsNothing);
    });

    testWidgets(
        'enableFullScreen: false leaves the tap to the surrounding row',
        (tester) async {
      var rowTaps = 0;
      await _pump(
        tester,
        GestureDetector(
          onTap: () => rowTaps++,
          child: const SimfLogoImage(
            url: 'https://example.test/logo.png',
            placeholder: Text('LOADING'),
            semanticLabel: 'SAMI',
            enableFullScreen: false,
          ),
        ),
      );

      await tester.tap(find.byType(SimfLogoImage));
      await tester.pumpAndSettle();

      expect(find.byType(SimfImageViewer), findsNothing);
      expect(rowTaps, 1);
    });
  });
}

Widget _initials() => const Text('SAMI-INITIALS');
