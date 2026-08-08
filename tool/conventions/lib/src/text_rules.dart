import 'config.dart';
import 'violation.dart';

/// `.razor` / `.css` rules. These surfaces have no AST here, so they are matched
/// textually — which is adequate because both rules are about literal syntax
/// (`style="…"`, `#rrggbb`) rather than structure.

/// A STATIC inline style: `style="..."` whose value carries no Razor
/// expression.
///
/// The rule targets styling that belongs in a stylesheet. It deliberately does
/// NOT match a value containing `@`, because a runtime value has nowhere else
/// to go: `style="--simf-gauge-fill:@Percent"` feeds a CSS custom property that
/// the stylesheet then consumes, which is the sanctioned way to pass data into
/// CSS. Banning it would force a class per pixel value, or an inline `<style>`
/// block, both worse. 16 of the 17 initial matches were this shape.
final RegExp _inlineStyle = RegExp(r'style\s*=\s*"([^"@]+)"');
final RegExp _hexColour = RegExp(r'#[0-9a-fA-F]{3,8}\b');

/// A `@* … *@` Razor comment or an HTML comment. Not code.
final RegExp _razorComment = RegExp(r'@\*.*?\*@|<!--.*?-->', dotAll: true);

/// A CSS comment.
final RegExp _cssComment = RegExp(r'/\*.*?\*/', dotAll: true);

List<Violation> analyseRazorFile({
  required String posixPath,
  required String content,
}) {
  final List<Violation> found = <Violation>[];
  final String stripped = content.replaceAll(_razorComment, '');
  final List<String> lines = stripped.split('\n');

  for (int i = 0; i < lines.length; i++) {
    for (final RegExpMatch match in _inlineStyle.allMatches(lines[i])) {
      found.add(
        Violation(
          rule: 'SIMF-N1',
          file: posixPath,
          line: i + 1,
          message: match.group(0)!,
          feature: Config.featureOf(posixPath),
          remedy: Remedy.stylesheet,
        ),
      );
    }
  }
  return found;
}

List<Violation> analyseCssFile({
  required String posixPath,
  required String content,
}) {
  if (Config.isAllowedFor('SIMF-N2', posixPath)) return const <Violation>[];

  final List<Violation> found = <Violation>[];
  final String stripped = content.replaceAll(_cssComment, '');
  final List<String> lines = stripped.split('\n');

  for (int i = 0; i < lines.length; i++) {
    for (final RegExpMatch match in _hexColour.allMatches(lines[i])) {
      found.add(
        Violation(
          rule: 'SIMF-N2',
          file: posixPath,
          line: i + 1,
          message: 'raw hex ${match.group(0)}',
          feature: Config.featureOf(posixPath),
          remedy: Remedy.themeTokensCss,
        ),
      );
    }
  }
  return found;
}
