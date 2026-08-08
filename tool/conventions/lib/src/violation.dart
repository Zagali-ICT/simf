/// One convention violation: a rule that fired at a source location.
class Violation {
  const Violation({
    required this.rule,
    required this.file,
    required this.line,
    required this.message,
    required this.feature,
    required this.remedy,
  });

  /// Rule id, e.g. `SIMF-C1`.
  final String rule;

  /// Repo-relative path with forward slashes, so a baseline recorded on Windows
  /// matches one recorded on the Linux/macOS agent.
  final String file;

  final int line;

  /// What was found, e.g. `SizedBox(height: 12)`.
  final String message;

  /// Grouping key for the report — the feature folder (`sessions`, `account`)
  /// or the surface (`ControlPanel`, `Website`). Mirrors how the external
  /// review grouped its findings, so the two documents can be read side by side.
  final String feature;

  /// Where this literal actually belongs. This is the part that teaches the
  /// taxonomy: not every number is a design token.
  final String remedy;

  /// Stable identity for the baseline.
  ///
  /// Deliberately EXCLUDES the line number: adding an import at the top of a
  /// file shifts every line below it, and a baseline keyed on line numbers
  /// would report the whole file as newly-violating.
  String get fingerprint => '$rule|$file|$message';

  Map<String, Object?> toJson() => <String, Object?>{
        'rule': rule,
        'file': file,
        'line': line,
        'message': message,
        'feature': feature,
        'remedy': remedy,
      };

  static Violation fromJson(Map<String, Object?> json) => Violation(
        rule: json['rule']! as String,
        file: json['file']! as String,
        line: json['line']! as int,
        message: json['message']! as String,
        feature: json['feature']! as String,
        remedy: json['remedy']! as String,
      );
}

/// Where a raw literal belongs. The external review prescribed `simfTokens` for
/// every number; that is wrong for four of these six cases, and following it
/// literally would put a backend-mandated validation limit in a design file.
abstract final class Remedy {
  /// Spacing, radius, icon size, opacity, colour, typography.
  static const String designToken =
      'SimfTokens (semantic name, not a value-name)';

  /// `maxLength` — must mirror the backend FluentValidation `.MaximumLength(N)`
  /// and EF `.HasMaxLength(N)`. A design-token file cannot carry that contract.
  static const String fieldLimit =
      'core/validation/field_limits.dart (mirror the backend MaximumLength)';

  /// Timeouts, cooldowns, debounces — behaviour policy, not design.
  static const String duration =
      'core/motion/motion_durations.dart (MotionDurations / TimeoutPolicy)';

  static const String endpoints =
      'features/<f>/data/*_endpoints.dart (D-545: the repo owns its path)';
  static const String assetUrls = 'core/net/asset_urls.dart';
  static const String appAssets = 'AppAssets (app/theme/app_assets.dart)';
  static const String localization = 'AppL10n';
  static const String ownFile = 'its own file under widgets/';
  static const String modelFile = 'a sibling *_models.dart';
  static const String sharedComponent = 'the shared NaviFormField';
  static const String themeTokensCss = 'theme.tokens.css (the CSS token SSOT)';
  static const String stylesheet = 'a BEM class in the stylesheet';
}
