import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/gallery/gallery_screen.dart';

const _items = <MediaItem>[
  MediaItem(
    id: 'm1',
    kind: MediaKind.image,
    title: 'Opening',
    album: 'Day 1',
  ),
  MediaItem(id: 'm2', kind: MediaKind.video, title: 'Keynote'),
];

Future<void> _pump(WidgetTester tester, List<Override> overrides) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: overrides,
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const GalleryScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('GalleryScreen (Page 030)', () {
    testWidgets('renders the media tiles', (tester) async {
      await _pump(tester, <Override>[
        mediaItemsProvider.overrideWith((ref) async => _items),
      ]);
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Keynote'), findsOneWidget);
      // The video tile shows a play icon.
      expect(find.byIcon(Icons.play_circle_outline), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        mediaItemsProvider.overrideWith((ref) async => const <MediaItem>[]),
      ]);
      expect(find.text('No media yet'), findsOneWidget);
    });

    testWidgets('a read failure shows the error state', (tester) async {
      await _pump(tester, <Override>[
        mediaItemsProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the media.'), findsOneWidget);
    });

    test('MediaKind.fromJson decodes int / name', () {
      expect(MediaKind.fromJson(1), MediaKind.video);
      expect(MediaKind.fromJson(0), MediaKind.image);
      expect(MediaKind.fromJson('Video'), MediaKind.video);
      expect(MediaKind.fromJson(null), MediaKind.image);
    });
  });
}
