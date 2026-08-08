import 'package:simf_conventions/src/dart_rules.dart';
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

    test('routes maxLines to a layout constant', () {
      expect(
        ofRule(run('Widget b() => T(maxLines: 2);'), 'SIMF-C1').single.remedy,
        Remedy.layoutIntent,
      );
    });

    test('routes crossAxisCount to the responsive layer', () {
      expect(
        ofRule(run('Widget b() => G(crossAxisCount: 3);'), 'SIMF-C1')
            .single
            .remedy,
        Remedy.responsive,
      );
    });

    // Regression: an unresolved AST parses `Duration(...)` as a MethodInvocation,
    // so a checker that only visits InstanceCreationExpression misses it.
    test('routes Duration to the timeout policy in BOTH call forms', () {
      for (final String form in <String>[
        'var d = Duration(seconds: 30);',
        'var d = const Duration(seconds: 30);',
      ]) {
        final List<Violation> found = ofRule(run(form), 'SIMF-C1');
        expect(found, hasLength(1), reason: form);
        expect(found.single.remedy, Remedy.duration, reason: form);
      }
    });

    test('fires on positional EdgeInsets values in both call forms', () {
      for (final String form in <String>[
        'var p = EdgeInsets.all(16);',
        'var p = const EdgeInsets.all(16);',
      ]) {
        expect(ofRule(run(form), 'SIMF-C1'), hasLength(1), reason: form);
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

    test('fires on a widget-building method', () {
      expect(
        ofRule(run('class A { Widget _buildContent() => X(); }'), 'SIMF-C3'),
        hasLength(1),
      );
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
}
