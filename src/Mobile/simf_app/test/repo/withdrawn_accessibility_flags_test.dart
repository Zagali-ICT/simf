import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// The four accessibility controls withdrawn on 2026-09-04 must have no reader
/// left under `lib/`.
///
/// Withdrawing the switches without withdrawing their EFFECTS is the trap this
/// pins, and the first attempt fell into it: the controls were the only call
/// sites that could turn these flags off, so a stored `true` went on swapping
/// the theme, announcing screens and hiding organiser captions with nothing
/// left to undo it. For a signed-in account it is worse than sticky, because
/// the account copy replays onto every fresh install.
///
/// The fields, the prefs keys and the server wire contract are deliberately
/// KEPT, so this asserts on the READ at the point of effect, not on the
/// setting's existence. When one is built for real, delete its entry here in
/// the same changeset that adds the reader back.
void main() {
  test('no withdrawn accessibility flag is read anywhere under lib/', () {
    const withdrawn = <String, String>{
      'highContrast':
          'selected SimfTheme.highContrast*(), which drops the brand Arabic '
              'font from buttons and loses the drawer row colour (D-549)',
      'reduceMotion':
          'wrote MediaQueryData.disableAnimations, which nothing here reads',
      'screenReaderAssist':
          'mounted ScreenAnnouncer, which announces on MOUNT, not on '
              'navigation',
      'captions': 'gated the organiser caption strip',
    };

    final offenders = <String>[];
    for (final file in Directory('lib')
        .listSync(recursive: true)
        .whereType<File>()
        .where((f) => f.path.endsWith('.dart'))) {
      final path = file.path.replaceAll(r'\', '/');

      // The feature's own data layer still declares, persists and syncs these
      // fields. That is the point: only the consumers are gone.
      if (path.contains('lib/features/accessibility/data/')) {
        continue;
      }

      // ScreenAnnouncer is ORPHANED, not wired: SimfPageShell no longer mounts
      // it and nothing else references it. Excluded rather than deleted,
      // because this change did not create the file. Delete it, or rebuild it
      // as a NavigatorObserver so it fires on navigation instead of on mount.
      if (path.endsWith('lib/app/widgets/screen_announcer.dart')) {
        continue;
      }

      // Strip line comments before scanning. The code that explains why a flag
      // is no longer read has to be able to name it.
      final source = file
          .readAsStringSync()
          .split('\n')
          .where((line) => !line.trimLeft().startsWith('//'))
          .join('\n');

      for (final entry in withdrawn.entries) {
        // `a11y.highContrast`, `settings.captions`, `.read(...).reduceMotion`
        // - a read is always a member access on the settings object.
        if (RegExp(r'\.' + entry.key + r'\b').hasMatch(source)) {
          offenders.add('$path: reads ${entry.key} (${entry.value})');
        }
      }
    }

    expect(
      offenders,
      isEmpty,
      reason: 'A withdrawn accessibility flag is read again. Its switch is '
          'gone, so nothing can turn it off:\n  ${offenders.join('\n  ')}',
    );
  });

  test('the accessibility screen renders no switch', () {
    // The screen is reachable SIGNED OUT, so an App Store reviewer needs no
    // credentials to find a control that does nothing.
    final source =
        File('lib/features/accessibility/accessibility_screen.dart')
            .readAsStringSync();
    expect(source.contains('AccessibilityToggleRow'), isFalse);
    expect(source.contains('AccessibilityScreenReaderRow'), isFalse);
  });
}
