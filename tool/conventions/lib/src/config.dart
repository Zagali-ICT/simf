import 'package:path/path.dart' as p;

/// Scan targets, exclusions and per-rule allowlists.
///
/// Every path here is repo-relative with forward slashes. Paths coming off the
/// filesystem are normalised through [relativePosix] before any comparison, so
/// the checker behaves identically on the Windows dev box and the build agent.
abstract final class Config {
  /// The Flutter app's Dart sources — the surface the external review covered.
  static const String flutterLib = 'src/Mobile/simf_app/lib';

  /// The .NET surfaces (`SIMF-N*` rules).
  static const List<String> razorRoots = <String>[
    'src/ControlPanel/SIMF.ControlPanel',
    'src/Website/SIMF.Web',
    'src/Shared/SIMF.Components',
  ];

  /// Never scanned. `third_party/` is the vendored `video_player_android`
  /// (D-768): upstream code we neither author nor own the style of, already
  /// excluded from `flutter analyze` for the same reason.
  static const List<String> excludedSegments = <String>[
    '/third_party/',
    '/build/',
    '/.dart_tool/',
    '/obj/',
    '/bin/',
    '/wwwroot/lib/', // vendored CSS/JS (bootstrap et al)
  ];

  static const List<String> excludedSuffixes = <String>[
    '.g.dart',
    '.freezed.dart',
    '.min.css',
  ];

  /// Files that are *allowed* to hold the literal a rule bans — the single
  /// source of truth each rule redirects to.
  static const Map<String, List<String>> ruleAllowlist = <String, List<String>>{
    'SIMF-C1': <String>['app/theme/tokens.dart'],
    'SIMF-C2': <String>['_endpoints.dart', 'asset_urls.dart', 'app_config.dart'],
    'SIMF-C4': <String>['app/localization/'],
    'SIMF-C5': <String>['app/theme/app_assets.dart'],
    'SIMF-C7': <String>['navi_form_field.dart'],
    'SIMF-N2': <String>['theme.tokens.css'],
  };

  static bool isExcluded(String posixPath) {
    final String probe = '/$posixPath';
    for (final String segment in excludedSegments) {
      if (probe.contains(segment)) return true;
    }
    for (final String suffix in excludedSuffixes) {
      if (posixPath.endsWith(suffix)) return true;
    }
    return false;
  }

  /// True when [posixPath] is the file this rule redirects *to*, and so may
  /// legitimately contain the literal.
  static bool isAllowedFor(String rule, String posixPath) {
    final List<String> allowed = ruleAllowlist[rule] ?? const <String>[];
    for (final String fragment in allowed) {
      if (posixPath.contains(fragment)) return true;
    }
    return false;
  }

  /// Tests carry deliberate literals (fixture sizes, expected values) and are
  /// not shipped UI. They are scanned for nothing.
  static bool isTest(String posixPath) =>
      posixPath.contains('/test/') ||
      posixPath.contains('/integration_test/') ||
      posixPath.endsWith('_test.dart');

  /// Repo-relative POSIX path, for stable fingerprints across platforms.
  static String relativePosix(String absolutePath, String repoRoot) =>
      p.posix.joinAll(p.split(p.relative(absolutePath, from: repoRoot)));

  /// Grouping key for the report — mirrors how the external review grouped its
  /// findings ("About feature", "Account feature", ...).
  static String featureOf(String posixPath) {
    const String featuresRoot = '$flutterLib/features/';
    if (posixPath.startsWith(featuresRoot)) {
      final String rest = posixPath.substring(featuresRoot.length);
      final int slash = rest.indexOf('/');
      return slash == -1 ? rest : rest.substring(0, slash);
    }
    if (posixPath.startsWith('$flutterLib/app/')) return 'app (shared shell)';
    if (posixPath.startsWith('$flutterLib/core/')) return 'core';
    if (posixPath.startsWith('src/ControlPanel/')) return 'ControlPanel';
    if (posixPath.startsWith('src/Website/')) return 'Website';
    if (posixPath.startsWith('src/Shared/')) return 'Shared components';
    return 'other';
  }
}
