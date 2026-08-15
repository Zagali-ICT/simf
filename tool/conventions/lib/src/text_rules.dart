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

/// A custom-property DEFINITION: `--name: #rrggbb`.
///
/// A stylesheet has to write the hex somewhere, and that somewhere is the token
/// block. Flagging the definition would make the rule unsatisfiable, exactly as
/// flagging a named Dart constant did for C1. The finding is a hex at a USE
/// site, which bypasses the token.
final RegExp _tokenDefinition = RegExp(r'--[\w-]+\s*:\s*#[0-9a-fA-F]{3,8}');

/// A custom property defined as a reference to ITSELF: `--navy: var(--navy)`.
///
/// CSS treats a self-referencing custom property as a dependency cycle and
/// resolves it to the guaranteed-invalid value. Every `var(--navy)` use site
/// then falls back to the inherited or initial value, so the colour is gone.
/// Nothing anywhere reports it: the stylesheet parses, the build is green, and
/// the page simply renders without its palette.
///
/// This rule exists because that happened. An automated hex -> token rewrite
/// produced exactly this shape across all 38 colour tokens of `landing.css`,
/// shipped through a PR to production, and flattened the entire public site.
/// It was invisible to N2 by construction: N2 counts raw hex at USE sites, so
/// deleting a palette drives its count to zero and reads as a clean sweep. A
/// rule that measures what is absent needs a companion that checks what is left.
///
/// The trailing `[,)]` catches the fallback form too — `--navy: var(--navy, #001640)`
/// looks defensive but is not. A var() inside a cycle does not fall back: the spec
/// makes the whole property invalid, so the fallback is never reached and the
/// colour is just as gone. Matching only `var(--navy)` would leave the more
/// plausible-looking of the two shapes unguarded.
final RegExp _selfReferencingToken =
    RegExp(r'--([\w-]+)\s*:\s*var\(\s*--([\w-]+)\s*[,)]');

List<Violation> analyseCssFile({
  required String posixPath,
  required String content,
}) {
  final List<Violation> found = <Violation>[];
  final String stripped = content.replaceAll(_cssComment, '');
  final List<String> lines = stripped.split('\n');

  // N3 is checked BEFORE the N2 allowlist and is never exempted by it. The two
  // rules are opposites: N2's allowlist names the files that are SUPPOSED to
  // hold literal colour (`theme.tokens.css`), and those are precisely the files
  // where a self-reference destroys the most.
  for (int i = 0; i < lines.length; i++) {
    for (final RegExpMatch match in _selfReferencingToken.allMatches(lines[i])) {
      if (match.group(1) != match.group(2)) continue;
      found.add(
        Violation(
          rule: 'SIMF-N3',
          file: posixPath,
          line: i + 1,
          message: 'self-referencing token --${match.group(1)}',
          feature: Config.featureOf(posixPath),
          remedy: Remedy.tokenLiteral,
        ),
      );
    }
  }

  if (Config.isAllowedFor('SIMF-N2', posixPath)) return found;

  for (int i = 0; i < lines.length; i++) {
    final String line = lines[i].replaceAll(_tokenDefinition, '');
    for (final RegExpMatch match in _hexColour.allMatches(line)) {
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
