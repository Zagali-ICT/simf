import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// The feature-shape ratchet — CLAUDE.md sections 1, 2 and 8.
///
/// Four structural rules that the repo already mostly satisfies, and that
/// nothing enforced. Each is the kind of drift a review catches on a good day
/// and misses on a busy one, so it is pinned mechanically instead:
///
///   1. A provider belongs in `data/`, never in a `*_screen.dart`. A provider
///      declared in a screen forces any other feature that needs it to import
///      a screen.
///   2. A widget class belongs in `widgets/`. A widget at a feature ROOT is
///      the violation section 1 names; the feature root is for screens and for
///      small purpose-named pure helpers.
///   3. A feature never imports `dio` or `http`. The transport lives in
///      `simf_data_pkg` (SIMF-MAA-001 section 5/6/9.1, D-545) — a feature
///      repository calls `SimfApiClient` and nothing lower.
///   4. No file over ~400 lines (section 1).
///
/// **THE RULE FOR THE LISTS BELOW: entries are removed as the work lands and
/// are NEVER added.** Every list was re-derived against the tree on
/// 2026-08-18, once the clean-code round had landed, by emptying it and
/// reading this file's own failure output — so each holds exactly the
/// offenders that exist now, the suite is green today, and a NEW offender
/// fails the build. Shrinking a list is the work; growing one is the defect
/// this file exists to catch. Nothing enforces the pruning — a stale entry is
/// inert, whereas a stale-entry check would redden the build for somebody
/// else's improvement, which is how a ratchet gets deleted instead of obeyed.
///
/// That inertness is why pruning is a MEASUREMENT and never a reading of the
/// previous list. Three of the four lists had gone empty without anything
/// saying so, and the fourth had lost five of its thirteen entries; a prune
/// driven by which entries somebody remembered moving would have kept most of
/// them.
///
/// The working directory for `flutter test` is the package root
/// (`src/Mobile/simf_app`), so every path below is relative to that.

/// Screens that still declare a top-level public provider, keyed
/// `<path under lib/features/> :: <symbol>`. **Empty on 2026-08-18** — all 24
/// moved into their feature's `data/` folder, so the rule now holds outright
/// and the first screen to re-declare one fails the build.
///
/// The list stays, empty, rather than being deleted with its last entry: it is
/// what makes the failure message name a KNOWN-offender allowance rather than
/// read as an unconditional ban somebody has to go and re-litigate.
///
/// Symbol-level, not file-level, deliberately: `live_broadcast_screen.dart`
/// held two, so a file-level entry would have kept guarding nothing once the
/// first one moved, and a SECOND provider added to an already-listed screen
/// would have slipped in free.
const List<String> _providersInScreens = <String>[];

/// Widget classes still declared at a feature root instead of in `widgets/`,
/// keyed `<path under lib/features/> :: <class>`. **Empty on 2026-08-18** —
/// the one holdout, `account/biometric_auth.dart :: FaceIdToggleTile`, is now
/// `account/widgets/face_id_toggle_tile.dart`, leaving that root file the pure
/// helper section 1 describes.
const List<String> _widgetsAtFeatureRoot = <String>[];

/// Feature files still importing the transport directly, keyed by path under
/// `lib/features/`. Empty on 2026-08-18 — the boundary is clean, and the list
/// exists so a first offender fails with the same message shape as the rest.
const List<String> _featuresImportingTransport = <String>[];

/// Files over 400 lines, keyed by path under `lib/`. 8 of them on 2026-08-18,
/// running from 457 to 2730 lines — down from 13 at 408 to 2769, the five
/// prunes being the two request sheets, `gate_scan_screen.dart`,
/// `identity_verification_screen.dart` and `session_detail_screen.dart`.
///
/// Paths only, no per-file line counts: pinning the counts would turn every
/// ordinary edit — including one that SHRINKS the file — into a failure.
const List<String> _oversizedFiles = <String>[
  'app/localization/app_l10n.dart',
  'app/router.dart',
  'app/theme/tokens.dart',
  'features/account/data/profile_models.dart',
  'features/account/sign_up_visitor_screen.dart',
  'features/sessions/data/seat_map_models.dart',
  'features/sessions/data/session_models.dart',
  'features/staff/register_visitor_screen.dart',
];

/// A top-level `final` at column 0 whose initializer starts with an
/// identifier. `^` under `multiLine` is what makes it top-level, and it also
/// means a commented-out declaration can never match.
///
/// `\s*` spans newlines, which matters here: the 80-column limit wraps most of
/// these declarations onto a second line, so a line-anchored match would miss
/// two thirds of them.
final RegExp _topLevelFinal =
    RegExp(r'^final\s+([a-zA-Z]\w*)\s*=\s*([A-Za-z_]\w*)', multiLine: true);

/// A widget class declaration, tolerant of the same wrap before `extends`.
final RegExp _widgetClass = RegExp(
  r'^class\s+(\w+)[^{]*?\bextends\s+'
  '(?:StatelessWidget|StatefulWidget|ConsumerWidget|ConsumerStatefulWidget)'
  r'\b',
  multiLine: true,
);

final RegExp _transportImport =
    RegExp('^import\\s+[\'"]package:(?:dio|http)/', multiLine: true);

/// Windows `Directory.listSync` returns backslash paths; normalise so the
/// comparisons below read the same on every platform.
String _posix(String path) => path.replaceAll(r'\', '/');

List<File> _dartFilesUnder(String dir) => Directory(dir)
    .listSync(recursive: true)
    .whereType<File>()
    .where((f) => f.path.endsWith('.dart'))
    .toList();

/// Path relative to `lib/features/`, e.g. `badge/badge_screen.dart`.
String _featureKey(File file) =>
    _posix(file.path).replaceFirst('lib/features/', '');

/// True for `lib/features/<feature>/<file>.dart` — the feature ROOT, one level
/// below the feature, not `widgets/` or `data/`.
bool _isFeatureRoot(File file) => _featureKey(file).split('/').length == 2;

/// The offenders that are NOT on the pinned list — the only direction any of
/// these tests asserts.
///
/// Set equality would be the obvious matcher and it is the wrong one: it fails
/// when an entry DISAPPEARS too, so the next agent to move a provider out of a
/// screen reddens this file, and the fix in front of them is to delete the
/// test. One-directional is also the readable failure — it names the new
/// offender instead of printing a 24-against-23 diff.
List<String> _notAllowed(List<String> offenders, List<String> allowed) =>
    offenders.where((offender) => !allowed.contains(offender)).toList()..sort();

void main() {
  group('CLAUDE.md 1 — a provider lives in data/, not in a screen', () {
    test('no top-level public provider is declared in a *_screen.dart', () {
      final offenders = <String>[];
      for (final file in _dartFilesUnder('lib/features')) {
        if (!file.path.endsWith('_screen.dart')) {
          continue;
        }
        final source = file.readAsStringSync();
        for (final match in _topLevelFinal.allMatches(source)) {
          final name = match.group(1)!;
          final initializer = match.group(2)!;
          if (!name.endsWith('Provider') && !initializer.contains('Provider')) {
            continue;
          }
          offenders.add('${_featureKey(file)} :: $name');
        }
      }

      expect(
        _notAllowed(offenders, _providersInScreens),
        isEmpty,
        reason: 'A provider declared in a screen forces every other feature '
            'that needs it to import a screen. Move it beside the repository '
            "that feeds it, in the feature's data/ folder — or, if only this "
            'screen reads it, make it private with a leading underscore.',
      );
    });
  });

  group('CLAUDE.md 1 — a widget lives in widgets/, not at a feature root', () {
    test('no widget class is declared in a feature-root file', () {
      final offenders = <String>[];
      for (final file in _dartFilesUnder('lib/features')) {
        // Screens sit at the feature root by convention (section 1) and are
        // widgets by definition, so they are the one exception here.
        if (!_isFeatureRoot(file) || file.path.endsWith('_screen.dart')) {
          continue;
        }
        for (final match in _widgetClass.allMatches(file.readAsStringSync())) {
          offenders.add('${_featureKey(file)} :: ${match.group(1)}');
        }
      }

      expect(
        _notAllowed(offenders, _widgetsAtFeatureRoot),
        isEmpty,
        reason: 'The feature root holds screens and small purpose-named pure '
            'helpers — functions and constants, never a widget. Move the '
            "widget into the feature's widgets/ folder, one public widget per "
            'file.',
      );
    });
  });

  group('CLAUDE.md 0 — the transport stays in simf_data_pkg', () {
    test('no file under lib/features/ imports package:dio or package:http',
        () {
      final offenders = <String>[];
      for (final file in _dartFilesUnder('lib/features')) {
        if (_transportImport.hasMatch(file.readAsStringSync())) {
          offenders.add(_featureKey(file));
        }
      }

      expect(
        _notAllowed(offenders, _featuresImportingTransport),
        isEmpty,
        reason: 'simf_data_pkg owns the one dio client and its interceptors '
            '(SIMF-MAA-001 section 5/6/9.1, D-545). A feature repository calls '
            'SimfApiClient; it never reaches past it to the HTTP library, and '
            'a widget never calls the network at all.',
      );
    });
  });

  group('CLAUDE.md 1 — no file over 400 lines', () {
    test('every file under lib/ is at most 400 lines', () {
      final offenders = <String>[];
      for (final file in _dartFilesUnder('lib')) {
        if (file.readAsLinesSync().length > 400) {
          offenders.add(_posix(file.path).replaceFirst('lib/', ''));
        }
      }

      expect(
        _notAllowed(offenders, _oversizedFiles),
        isEmpty,
        reason: 'A file this long has stopped having one subject. Split it '
            'along the seam it already has — a screen into widgets/, a model '
            'file per aggregate — rather than shredding it to hit a number.',
      );
    });
  });
}
