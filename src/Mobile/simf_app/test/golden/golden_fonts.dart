import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// Shared golden-render font loader (owner 2026-06-28, verification option 2).
///
/// Golden tests render a screen at its exact Figma frame size so the generated
/// PNG can be compared pixel-for-pixel against the Figma frame. For that to be
/// faithful the real brand fonts must be loaded, otherwise Arabic text falls
/// back to boxes/Ahem. Call this once from `setUpAll`.
///
/// Loads the bundled brand faces (FSAlbertArabic / Cairo / Inter) from the
/// app's declared font assets, plus MaterialIcons from the Flutter SDK cache
/// (it ships with the SDK, not as an app asset) so `Icons.*` glyphs render, not
/// boxes.
Future<void> loadGoldenFonts() async {
  Future<void> load(String family, List<String> assets) async {
    final loader = FontLoader(family);
    for (final path in assets) {
      loader.addFont(rootBundle.load(path));
    }
    await loader.load();
  }

  await load('FSAlbertArabic', <String>[
    'assets/fonts/fs-albert-arabic-web-regular.ttf',
    'assets/fonts/fs-albert-arabic-web-bold.ttf',
    'assets/fonts/fs-albert-arabic-web-extrabold.ttf',
  ]);
  await load('Cairo', <String>['assets/fonts/Cairo.ttf']);
  await load('Inter', <String>['assets/fonts/Inter.ttf']);

  for (final candidate in <String>[
    r'D:\dev\flutter\bin\cache\artifacts\material_fonts\materialicons-regular.otf',
    r'D:\dev\flutter\bin\cache\artifacts\material_fonts\MaterialIcons-Regular.otf',
  ]) {
    final file = File(candidate);
    if (file.existsSync()) {
      final bytes = await file.readAsBytes();
      final loader = FontLoader('MaterialIcons')
        ..addFont(Future<ByteData>.value(ByteData.sublistView(bytes)));
      await loader.load();
      break;
    }
  }
}
