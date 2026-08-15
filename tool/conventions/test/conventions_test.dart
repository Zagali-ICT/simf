import 'dart:io';

import 'package:path/path.dart' as p;
import 'package:simf_conventions/src/dart_rules.dart';
import 'package:simf_conventions/src/repo_root.dart';
import 'package:simf_conventions/src/text_rules.dart';
import 'package:simf_conventions/src/violation.dart';
import 'package:test/test.dart';

const String _featurePath = 'src/Mobile/simf_app/lib/features/demo/demo.dart';

List<Violation> run(String code, {String path = _featurePath}) =>
    analyseDartFile(posixPath: path, content: code);

List<Violation> ofRule(List<Violation> all, String rule) =>
    all.where((Violation v) => v.rule == rule).toList();

void main() {
  group('SIMF-C1 numeric literals', () {
    test('fires on a design-quantity named argument', () {
      final List<Violation> found =
          ofRule(run('Widget b() => SizedBox(height: 12);'), 'SIMF-C1');
      expect(found, hasLength(1));
      expect(found.single.remedy, Remedy.designToken);
    });

    test('permits 0 and 1 as identity values', () {
      expect(
        ofRule(run('Widget b() => Opacity(opacity: 0, child: X(height: 1));'),
            'SIMF-C1'),
        isEmpty,
      );
    });

    test('routes maxLength to the field-limit file, NOT to design tokens', () {
      final List<Violation> found =
          ofRule(run('Widget b() => F(maxLength: 100);'), 'SIMF-C1');
      expect(found.single.remedy, Remedy.fieldLimit);
    });

    // Deliberately silent: `maxLines: 2` already says "at most two lines", and
    // crossAxisCount is a responsive DESIGN change raised separately rather
    // than smuggled into a cleanup wave.
    test('does NOT fire on maxLines or crossAxisCount', () {
      expect(ofRule(run('Widget b() => T(maxLines: 2);'), 'SIMF-C1'), isEmpty);
      expect(
        ofRule(run('Widget b() => G(crossAxisCount: 3);'), 'SIMF-C1'),
        isEmpty,
      );
    });

    // Regression: an unresolved AST parses `Duration(...)` as a MethodInvocation,
    // so a checker that only visits InstanceCreationExpression misses it.
    test('routes Duration to the timeout policy in BOTH call forms', () {
      for (final String form in <String>[
        'Widget b() => A(duration: Duration(seconds: 30));',
        'Widget b() => A(duration: const Duration(seconds: 30));',
      ]) {
        final List<Violation> found = ofRule(run(form), 'SIMF-C1');
        expect(found, hasLength(1), reason: form);
        expect(found.single.remedy, Remedy.duration, reason: form);
      }
    });

    test('fires on positional EdgeInsets values in both call forms', () {
      for (final String form in <String>[
        'Widget b() => A(padding: EdgeInsets.all(16));',
        'Widget b() => A(padding: const EdgeInsets.all(16));',
      ]) {
        expect(ofRule(run(form), 'SIMF-C1'), hasLength(1), reason: form);
      }
    });

    // A value that ALREADY has a name is not a magic number - it is the fix.
    // Flagging `const Duration saudiOffset = Duration(hours: 3);` would make the
    // rule unsatisfiable, because that declaration is what resolving one looks
    // like.
    test('is silent when the literal already has a name', () {
      for (final String form in <String>[
        'const Duration saudiOffset = Duration(hours: 3);',
        'class A { static const double tileSize = 48; }',
        'class A { const A({this.tick = const Duration(seconds: 15)}); '
            'final Duration tick; }',
      ]) {
        expect(ofRule(run(form), 'SIMF-C1'), isEmpty, reason: form);
      }
    });

    test('is silent inside tokens.dart, which defines the values', () {
      expect(
        ofRule(
          run('const double x = 12;\nWidget b() => SizedBox(height: 12);',
              path: 'src/Mobile/simf_app/lib/app/theme/tokens.dart'),
          'SIMF-C1',
        ),
        isEmpty,
      );
    });
  });

  group('baseline fingerprints', () {
    // A finding must keep its identity across edits that move it. The C3
    // message carries the file's LINE COUNT, so deleting unused imports
    // renamed every fingerprint in a file at once and the gate reported 14
    // "new" violations in code nobody had touched.
    test('survive a change in the line count the message reports', () {
      const String decl = 'class A { Widget _buildContent() => X(); }';
      String fingerprintAt(int padding) {
        final String src =
            '${List<String>.filled(padding, '// pad').join('\n')}\n$decl';
        return ofRule(run(src), 'SIMF-C3').single.fingerprint;
      }

      expect(fingerprintAt(420), fingerprintAt(500));
    });

    test('still separate two different findings in one file', () {
      final List<Violation> found = ofRule(
        run('${List<String>.filled(420, '// pad').join('\n')}\n'
            'class A { Widget _buildOne() => X(); Widget _buildTwo() => Y(); }'),
        'SIMF-C3',
      );
      expect(found, hasLength(2));
      expect(found[0].fingerprint, isNot(found[1].fingerprint));
    });
  });

  group('SIMF-C2 endpoint and asset URLs', () {
    test('fires on an endpoint path literal', () {
      final List<Violation> found =
          ofRule(run("var r = client.get('/app/news');"), 'SIMF-C2');
      expect(found.single.remedy, Remedy.endpoints);
    });

    test('fires on an interpolated asset URL', () {
      final List<Violation> found = ofRule(
        run("var u = '\$baseUrl/app/assets/NewsImage/\${item.id}/image';"),
        'SIMF-C2',
      );
      expect(found.single.remedy, Remedy.assetUrls);
    });

    // The reason this is AST-based rather than grep-based: every repository has
    // `/// GET /app/news` in its doc header, and a text search flags all of them.
    test('does NOT fire on a path inside a doc comment', () {
      expect(
        ofRule(run('/// GET /app/news returns the list.\nvoid f() {}'),
            'SIMF-C2'),
        isEmpty,
      );
    });

    test('is silent inside an endpoints file', () {
      expect(
        ofRule(
          run("const String list = '/app/news';",
              path:
                  'src/Mobile/simf_app/lib/features/news/data/news_endpoints.dart'),
          'SIMF-C2',
        ),
        isEmpty,
      );
    });
  });

  group('SIMF-C3 declaration placement', () {
    test('fires on a private widget class', () {
      expect(
        ofRule(run('class _Card extends StatelessWidget {}'), 'SIMF-C3'),
        hasLength(1),
      );
    });

    // `_FooState extends State<Foo>` is mandated by the framework. Flagging it
    // would make the rule impossible to satisfy for any StatefulWidget.
    test('does NOT fire on a State subclass', () {
      expect(
        ofRule(run('class _FooState extends State<Foo> {}'), 'SIMF-C3'),
        isEmpty,
      );
    });

    // A _build* method is composition in a small file and a symptom in a huge
    // one. All 78 in this repo read instance state, so none is a mechanical
    // extraction; the defect the rule should catch is the oversized file.
    test('fires on a widget-building method only in an oversized file', () {
      const String decl = 'class A { Widget _buildContent() => X(); }';
      expect(ofRule(run(decl), 'SIMF-C3'), isEmpty);
      final String padded =
          '${List<String>.filled(420, '// pad').join('\n')}\n$decl';
      expect(ofRule(run(padded), 'SIMF-C3'), hasLength(1));
    });
  });

  group('SIMF-C4 user-facing strings', () {
    // Regression: `Text('x')` is a MethodInvocation in an unresolved AST.
    test('fires on a literal in Text() in both call forms', () {
      for (final String form in <String>[
        "Widget b() => Text('Welcome');",
        "Widget b() => const Text('Welcome');",
      ]) {
        expect(ofRule(run(form), 'SIMF-C4'), hasLength(1), reason: form);
      }
    });

    test('ignores separators and punctuation', () {
      expect(ofRule(run("Widget b() => Text(':');"), 'SIMF-C4'), isEmpty);
      expect(ofRule(run("Widget b() => Text('12');"), 'SIMF-C4'), isEmpty);
    });

    // Hand-rolled localization: the reviewer flagged exactly this line in
    // live_badges.dart. Both branches are copy that belongs in AppL10n.
    test('fires on BOTH branches of a ternary language switch', () {
      expect(
        ofRule(
          run("Widget b() => Text(isArabic ? 'العربية' : 'English');"),
          'SIMF-C4',
        ),
        hasLength(2),
      );
    });

    test('fires on a hint/label named argument', () {
      expect(
        ofRule(run("Widget b() => F(hintText: 'Enter your email');"),
            'SIMF-C4'),
        hasLength(1),
      );
    });
  });

  group('SIMF-C5 / C6 / C7', () {
    test('C5 fires on a bundled asset path', () {
      expect(
        ofRule(run("var i = 'assets/icons/ic_caret_left.svg';"), 'SIMF-C5')
            .single
            .remedy,
        Remedy.appAssets,
      );
    });

    test('C6 fires on a JSON model inside a repository file', () {
      const String code = '''
class LiveSession {
  factory LiveSession.fromJson(Map<String, dynamic> j) => LiveSession();
}
class LiveRepository {}
''';
      final List<Violation> found = ofRule(
        run(code,
            path:
                'src/Mobile/simf_app/lib/features/live/data/live_repository.dart'),
        'SIMF-C6',
      );
      expect(found, hasLength(1));
      expect(found.single.message, contains('LiveSession'));
    });

    // Regression: this returned 0 across the whole repository because
    // `TextFormField(...)` is a MethodInvocation without `new`/`const`.
    test('C7 fires on a raw TextFormField in both call forms', () {
      for (final String form in <String>[
        'Widget b() => TextFormField(controller: c);',
        'Widget b() => new TextFormField(controller: c);',
      ]) {
        expect(
          ofRule(
            run(form,
                path: 'src/Mobile/simf_app/lib/features/demo/demo_screen.dart'),
            'SIMF-C7',
          ),
          hasLength(1),
          reason: form,
        );
      }
    });

    // A field component wrapping TextFormField IS the shared component. Firing
    // here would make the rule unsatisfiable: something has to wrap it.
    test('C7 does NOT fire inside a field component', () {
      for (final String path in <String>[
        'src/Mobile/simf_app/lib/features/account/widgets/mobile_field.dart',
        'src/Mobile/simf_app/lib/core/widgets/simf_field_style.dart',
      ]) {
        expect(
          ofRule(run('Widget b() => TextFormField(controller: c);', path: path),
              'SIMF-C7'),
          isEmpty,
          reason: path,
        );
      }
    });
  });

  group('SIMF-N1 / N2 .NET surfaces', () {
    test('N1 fires on an inline style attribute', () {
      final List<Violation> found = analyseRazorFile(
        posixPath: 'src/ControlPanel/SIMF.ControlPanel/X.razor',
        content: '<div style="color:red">hi</div>',
      );
      expect(found, hasLength(1));
    });

    // A runtime value has nowhere else to live: the stylesheet consumes the
    // custom property. Flagging it would force a class per pixel value.
    test('N1 does NOT fire on a style carrying a Razor expression', () {
      for (final String markup in <String>[
        '<div style="--simf-gauge-fill:@Percent"></div>',
        '<div style="background-image:url(\'@s.Image\')"></div>',
      ]) {
        expect(
          analyseRazorFile(
            posixPath: 'src/Website/SIMF.Web/X.razor',
            content: markup,
          ),
          isEmpty,
          reason: markup,
        );
      }
    });

    test('N1 ignores a Razor comment', () {
      expect(
        analyseRazorFile(
          posixPath: 'src/ControlPanel/SIMF.ControlPanel/X.razor',
          content: '@* <div style="color:red"> *@',
        ),
        isEmpty,
      );
    });

    test('N2 fires on raw hex outside the token file', () {
      expect(
        analyseCssFile(
          posixPath: 'src/Website/SIMF.Web/wwwroot/css/landing.css',
          content: '.a { color: #ff0000; }',
        ),
        hasLength(1),
      );
    });

    // A stylesheet must write the hex SOMEWHERE, and that somewhere is its
    // token block. Same principle as C1 skipping a named Dart constant.
    test('N2 fires on a USE site but not on a token definition', () {
      const String path = 'src/Website/SIMF.Web/wwwroot/css/landing.css';
      expect(
        analyseCssFile(
          posixPath: path,
          content: ':root { --gold: #e8c060; }',
        ),
        isEmpty,
      );
      expect(
        analyseCssFile(
          posixPath: path,
          content: '.ln-chip { background: #e8c060; }',
        ),
        hasLength(1),
      );
    });

    test('N2 is silent inside theme.tokens.css, the token SSOT', () {
      expect(
        analyseCssFile(
          posixPath: 'src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css',
          content: ':root { --c: #ff0000; }',
        ),
        isEmpty,
      );
    });
  });

  // The regression these rules exist for. A hex -> token rewrite turned all 38
  // colour tokens of landing.css into `--navy: var(--navy)`, which CSS resolves
  // to the guaranteed-invalid value. The public site shipped with no palette,
  // and N2 read it as a clean sweep because deleting a palette removes every
  // raw hex it counts.
  group('SIMF-N3 self-referencing tokens', () {
    const String landing = 'src/Website/SIMF.Web/wwwroot/css/landing.css';

    test('fires on a token defined as its own name', () {
      final List<Violation> found = ofRule(
        analyseCssFile(
          posixPath: landing,
          content: '.landing { --navy: var(--navy); }',
        ),
        'SIMF-N3',
      );
      expect(found, hasLength(1));
      expect(found.single.message, contains('--navy'));
      expect(found.single.remedy, Remedy.tokenLiteral);
    });

    test('tolerates whitespace inside the self-reference', () {
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '.landing {\n  --gold :  var( --gold ) ;\n}',
          ),
          'SIMF-N3',
        ),
        hasLength(1),
      );
    });

    // A var() inside a cycle does NOT fall back — the spec invalidates the whole
    // property — so this shape is exactly as broken as the bare one, and reads as
    // more careful. It has to fire.
    test('fires on the fallback form, which does not actually fall back', () {
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '.landing { --navy: var(--navy, #001640); }',
          ),
          'SIMF-N3',
        ),
        hasLength(1),
      );
    });

    test('is silent when a token falls back to a DIFFERENT token', () {
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '.landing { --brand: var(--primary, #244a77); }',
          ),
          'SIMF-N3',
        ),
        isEmpty,
      );
    });

    test('is silent when a token references a DIFFERENT token', () {
      // The legitimate shape the refactor was aiming for: an alias. This must
      // stay legal or the rule bans `--bs-primary: var(--primary)`.
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '.landing { --bs-primary: var(--primary); }',
          ),
          'SIMF-N3',
        ),
        isEmpty,
      );
    });

    test('is silent on a definition holding a real literal', () {
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '.landing { --navy: #001640; }',
          ),
          'SIMF-N3',
        ),
        isEmpty,
      );
    });

    // N2's allowlist names the files that are SUPPOSED to hold literal colour.
    // Those are exactly the files where a self-reference does the most damage,
    // so N3 must not inherit that exemption.
    test('fires inside theme.tokens.css, which N2 exempts', () {
      const String tokens =
          'src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css';
      expect(
        ofRule(
          analyseCssFile(
            posixPath: tokens,
            content: ':root { --color-primary: var(--color-primary); }',
          ),
          'SIMF-N3',
        ),
        hasLength(1),
      );
      // ...and N2 stays exempt there, so the two rules are independent.
      expect(
        ofRule(
          analyseCssFile(posixPath: tokens, content: ':root { --c: #ff0000; }'),
          'SIMF-N2',
        ),
        isEmpty,
      );
    });

    test('ignores the pattern inside a CSS comment', () {
      expect(
        ofRule(
          analyseCssFile(
            posixPath: landing,
            content: '/* never write --navy: var(--navy) here */\n'
                '.landing { --navy: #001640; }',
          ),
          'SIMF-N3',
        ),
        isEmpty,
      );
    });
  });

  group('repository-root inference', () {
    // The checker printed "No violations found" from inside a git WORKTREE,
    // where `.git` is a pointer FILE rather than a directory: the walk-up found
    // no root, fell back to the working directory, scanned a tree with no
    // sources under it, and the CI gate passed on an empty scan. These pin both
    // shapes so the gate cannot go quiet that way again.
    test('finds a root whose .git is a directory (an ordinary clone)', () {
      final Directory tmp = Directory.systemTemp.createTempSync('simfroot');
      addTearDown(() => tmp.deleteSync(recursive: true));
      final String root = tmp.resolveSymbolicLinksSync();
      Directory(p.join(root, '.git')).createSync();
      final Directory deep = Directory(p.join(root, 'tool', 'conventions'))
        ..createSync(recursive: true);

      expect(inferRepoRoot(deep, fallback: 'FALLBACK'), root);
    });

    test('finds a root whose .git is a FILE (a git worktree)', () {
      final Directory tmp = Directory.systemTemp.createTempSync('simfwt');
      addTearDown(() => tmp.deleteSync(recursive: true));
      final String root = tmp.resolveSymbolicLinksSync();
      File(p.join(root, '.git')).writeAsStringSync('gitdir: /somewhere/.git\n');
      final Directory deep = Directory(p.join(root, 'tool', 'conventions'))
        ..createSync(recursive: true);

      expect(inferRepoRoot(deep, fallback: 'FALLBACK'), root);
    });

    test('falls back only when there is no marker at all', () {
      final Directory tmp = Directory.systemTemp.createTempSync('simfnone');
      addTearDown(() => tmp.deleteSync(recursive: true));
      final Directory deep = Directory(p.join(tmp.path, 'a', 'b'))
        ..createSync(recursive: true);

      expect(inferRepoRoot(deep, fallback: 'FALLBACK'), 'FALLBACK');
    });
  });
}
