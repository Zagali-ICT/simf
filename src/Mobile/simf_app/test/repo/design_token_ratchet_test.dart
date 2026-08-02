import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

// `#16` — the design-token ratchet.
//
// The sweep's rule is that `lib/app/theme/tokens.dart` is the ONLY place a
// colour literal or a bare text style may be written; every widget reads a
// named token. That held nowhere except by review, so it kept drifting back.
// These tests make it mechanical:
//
//   1. No raw colour anywhere under `lib/` outside `tokens.dart` — no
//      `Color(0x…)`, no `Colors.<name>`.
//   2. No inline `TextStyle(` in the swept files. The shared `app/widgets`
//      layer is in scope (owner Q11, 2026-07-30), so the shell, the drawer and
//      the dialogs are pinned here. `_sweptFiles` is a ratchet: entries are
//      added as the sweep reaches more files and are never removed.
//
// The working directory for `flutter test` is the package root
// (`src/Mobile/simf_app`), so every path below is relative to that.

/// The single file allowed to hold colour literals.
const String _tokensFile = 'lib/app/theme/tokens.dart';

/// Files pinned at zero inline `TextStyle(`. The shared layer the owner put in
/// scope, plus the feature files the sweep closed with it.
const List<String> _sweptFiles = <String>[
  'lib/app/widgets/simf_page_shell.dart',
  'lib/app/widgets/more_drawer.dart',
  'lib/app/widgets/simf_confirm_dialog.dart',
  'lib/app/widgets/simf_info_dialog.dart',
  'lib/features/more/widgets/more_list.dart',
  'lib/features/accessibility/widgets/accessibility_font_size_card.dart',
  'lib/features/accessibility/widgets/accessibility_toggle_row.dart',
  'lib/features/accessibility/widgets/accessibility_section_heading.dart',
];

final RegExp _rawColor = RegExp(r'Color\(0x|Colors\.[a-zA-Z]');
final RegExp _inlineTextStyle = RegExp(r'\bTextStyle\(');

List<File> _libDartFiles() => Directory('lib')
    .listSync(recursive: true)
    .whereType<File>()
    .where((f) => f.path.endsWith('.dart'))
    .toList();

/// Windows `Directory.listSync` returns backslash paths; normalise so the
/// comparisons below read the same on every platform.
String _posix(String path) => path.replaceAll('\\', '/');

void main() {
  group('#16 — raw colours live only in tokens.dart', () {
    test('no Color(0x…) / Colors.<name> anywhere else under lib/', () {
      final offenders = <String>[];
      for (final file in _libDartFiles()) {
        final path = _posix(file.path);
        if (path.endsWith(_tokensFile)) {
          continue;
        }
        for (final (index, line) in file.readAsLinesSync().indexed) {
          // Skip prose: several widgets NAME a token in a doc comment.
          final code = line.trimLeft();
          if (code.startsWith('//') || code.startsWith('///')) {
            continue;
          }
          if (_rawColor.hasMatch(line)) {
            offenders.add('$path:${index + 1}: ${line.trim()}');
          }
        }
      }

      expect(
        offenders,
        isEmpty,
        reason: 'Colour literals belong in $_tokensFile only — a raw colour in '
            'a widget cannot be re-themed and drifts from the Figma palette. '
            'Add the token first, then reference it:\n${offenders.join('\n')}',
      );
    });
  });

  group('#16 — the swept files carry no inline TextStyle', () {
    for (final path in _sweptFiles) {
      test('$path uses named SimfTokens styles only', () {
        final file = File(path);
        expect(
          file.existsSync(),
          isTrue,
          reason: '$path is on the swept list but does not exist. If it was '
              'renamed, update the list — do not drop the entry.',
        );

        final offenders = <String>[];
        for (final (index, line) in file.readAsLinesSync().indexed) {
          final code = line.trimLeft();
          if (code.startsWith('//') || code.startsWith('///')) {
            continue;
          }
          if (_inlineTextStyle.hasMatch(line)) {
            offenders.add('${index + 1}: ${line.trim()}');
          }
        }

        expect(
          offenders,
          isEmpty,
          reason: 'An inline TextStyle re-states values the design system '
              'already owns, so the two drift. Use a named SimfTokens style '
              '(add one to $_tokensFile if it is missing):\n'
              '${offenders.join('\n')}',
        );
      });
    }
  });
}
