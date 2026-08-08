import 'package:analyzer/dart/analysis/results.dart';
import 'package:analyzer/dart/analysis/utilities.dart';
import 'package:analyzer/dart/ast/ast.dart';
import 'package:analyzer/dart/ast/visitor.dart';
import 'package:analyzer/source/line_info.dart';

import 'config.dart';
import 'violation.dart';

/// Parses one Dart file and collects `SIMF-C*` violations.
///
/// Parsing is SYNTAX-ONLY (no type resolution): rules match on names, not
/// resolved types. That keeps a full-repo scan to a couple of seconds and means
/// the checker runs without `pub get` on the app. The trade-off is that a class
/// named `TextFormField` from some other library would be matched too — which
/// has not happened, and would be caught in review.
///
/// The important consequence of using the AST rather than grep: a path inside a
/// doc comment (`/// GET /app/news`) is NOT a string literal, so it does not
/// fire. A regex-based checker would report every repository's doc header.
List<Violation> analyseDartFile({
  required String posixPath,
  required String content,
}) {
  final ParseStringResult parsed = parseString(
    content: content,
    path: posixPath,
    throwIfDiagnostics: false,
  );
  final _RuleVisitor visitor = _RuleVisitor(
    posixPath: posixPath,
    lineInfo: parsed.lineInfo,
  );
  parsed.unit.accept(visitor);
  return visitor.violations;
}

/// Named arguments whose value is a design quantity — these belong in
/// `SimfTokens` under a SEMANTIC name.
const Set<String> _designNumberArgs = <String>{
  'height', 'width', 'size', 'fontSize', 'letterSpacing', 'wordSpacing',
  'elevation', 'strokeWidth', 'borderWidth', 'blurRadius', 'spreadRadius',
  'minWidth', 'maxWidth', 'minHeight', 'maxHeight', 'iconSize', 'splashRadius',
  'thickness', 'indent', 'endIndent', 'radius', 'opacity', 'runSpacing',
  'spacing', 'toolbarHeight', 'leadingWidth', 'itemExtent', 'mainAxisExtent',
  'maxCrossAxisExtent', 'childAspectRatio', 'aspectRatio', 'scale',
};

/// Named arguments that are NOT design quantities. The external review told us
/// to put all of these in `simfTokens`; each actually belongs somewhere else.
const Map<String, String> _nonDesignNumberArgs = <String, String>{
  'maxLength': Remedy.fieldLimit,
  'maxLines': Remedy.layoutIntent,
  'minLines': Remedy.layoutIntent,
  'crossAxisCount': Remedy.responsive,
};

/// Constructors whose POSITIONAL numeric arguments are design quantities.
const Set<String> _positionalDesignCtors = <String>{
  'EdgeInsets', 'EdgeInsetsDirectional', 'BorderRadius', 'Radius', 'Size',
  'Offset', 'BorderRadiusDirectional',
};

const Set<String> _widgetSupertypes = <String>{
  'StatelessWidget', 'StatefulWidget', 'ConsumerWidget',
  'ConsumerStatefulWidget', 'HookWidget', 'HookConsumerWidget',
};

/// Named arguments that carry user-visible copy.
const Set<String> _userFacingTextArgs = <String>{
  'hintText', 'labelText', 'helperText', 'errorText', 'tooltip',
  'semanticLabel', 'title', 'label', 'subtitle',
};

class _RuleVisitor extends RecursiveAstVisitor<void> {
  _RuleVisitor({required this.posixPath, required this.lineInfo});

  final String posixPath;
  final LineInfo lineInfo;
  final List<Violation> violations = <Violation>[];

  late final String _feature = Config.featureOf(posixPath);
  late final bool _isRepositoryFile = posixPath.endsWith('_repository.dart');

  void _add({
    required String rule,
    required int offset,
    required String message,
    required String remedy,
  }) {
    if (Config.isAllowedFor(rule, posixPath)) return;
    violations.add(
      Violation(
        rule: rule,
        file: posixPath,
        line: lineInfo.getLocation(offset).lineNumber,
        message: message,
        feature: _feature,
        remedy: remedy,
      ),
    );
  }

  /// The literal's numeric value, or null when it is not a plain number.
  /// `0` and `1` are allowed everywhere: they are identity/neutral values
  /// (`opacity: 0`, `maxLines: 1`), not design decisions.
  num? _numericValue(Expression expression) {
    if (expression is IntegerLiteral) return expression.value;
    if (expression is DoubleLiteral) return expression.value;
    if (expression is PrefixExpression && expression.operator.lexeme == '-') {
      final num? inner = _numericValue(expression.operand);
      return inner == null ? null : -inner;
    }
    return null;
  }

  bool _isSignificant(num? value) => value != null && value != 0 && value != 1;

  /// True when this literal is ALREADY the value of something with a name.
  ///
  /// `const Duration saudiOffset = Duration(hours: 3);` and
  /// `this.tickInterval = const Duration(seconds: 15)` are not magic numbers:
  /// they are the named constant and the named default the rule is asking for.
  /// Flagging them makes the rule unsatisfiable, because the fix for a magic
  /// number is to write exactly this. Only a literal used inline at a call site,
  /// `duration: const Duration(milliseconds: 250)`, is a finding.
  bool _hasOwnName(AstNode node) {
    AstNode? current = node.parent;
    while (current != null) {
      if (current is VariableDeclaration || current is DefaultFormalParameter) {
        return true;
      }
      // Stop at the first node that means "used here", rather than "declared as".
      if (current is Statement ||
          current is ClassMember && current is! FieldDeclaration ||
          current is CompilationUnit) {
        return false;
      }
      current = current.parent;
    }
    return false;
  }

  // ── SIMF-C1 / C7 / C4 ───────────────────────────────────────────────────
  //
  // Both visitors funnel into [_checkConstructorLike]. This matters: because the
  // AST is UNRESOLVED, `TextFormField(...)` parses as a MethodInvocation, not an
  // InstanceCreationExpression — only the `new` and `const` forms produce the
  // latter. Checking just one node type silently misses most call sites.
  @override
  void visitInstanceCreationExpression(InstanceCreationExpression node) {
    _checkConstructorLike(node.constructorName.toSource(), node.argumentList);
    super.visitInstanceCreationExpression(node);
  }

  @override
  void visitMethodInvocation(MethodInvocation node) {
    final Expression? target = node.target;
    final String source = target == null
        ? node.methodName.name
        : '${target.toSource()}.${node.methodName.name}';
    _checkConstructorLike(source, node.argumentList);
    super.visitMethodInvocation(node);
  }

  void _checkConstructorLike(String ctor, ArgumentList argumentList) {
    final String typeName = ctor.split('.').first;

    if (typeName == 'TextFormField') {
      _add(
        rule: 'SIMF-C7',
        offset: argumentList.offset,
        message: 'raw TextFormField',
        remedy: Remedy.sharedComponent,
      );
    }

    if (typeName == 'Duration') {
      for (final Expression arg in argumentList.arguments) {
        if (arg is NamedExpression &&
            _isSignificant(_numericValue(arg.expression)) &&
            !_hasOwnName(arg)) {
          _add(
            rule: 'SIMF-C1',
            offset: arg.offset,
            message: 'Duration(${arg.name.label.name}: ${arg.expression})',
            remedy: Remedy.duration,
          );
        }
      }
    }

    if (_positionalDesignCtors.contains(typeName)) {
      for (final Expression arg in argumentList.arguments) {
        if (arg is NamedExpression) continue;
        if (_isSignificant(_numericValue(arg)) && !_hasOwnName(arg)) {
          _add(
            rule: 'SIMF-C1',
            offset: arg.offset,
            message: '$ctor($arg)',
            remedy: Remedy.designToken,
          );
        }
      }
    }

    if (typeName == 'Text') {
      final Iterable<Expression> positional =
          argumentList.arguments.where((Expression a) => a is! NamedExpression);
      if (positional.isNotEmpty) {
        _flagUserFacingString(positional.first, 'Text(...)');
      }
    }
  }

  @override
  void visitNamedExpression(NamedExpression node) {
    final String name = node.name.label.name;
    final Expression value = node.expression;

    if (_designNumberArgs.contains(name) &&
        _isSignificant(_numericValue(value)) &&
        !_hasOwnName(node)) {
      _add(
        rule: 'SIMF-C1',
        offset: node.offset,
        message: '$name: $value',
        remedy: Remedy.designToken,
      );
    }

    final String? special = _nonDesignNumberArgs[name];
    if (special != null &&
        _isSignificant(_numericValue(value)) &&
        !_hasOwnName(node)) {
      _add(
        rule: 'SIMF-C1',
        offset: node.offset,
        message: '$name: $value',
        remedy: special,
      );
    }

    if (_userFacingTextArgs.contains(name)) {
      _flagUserFacingString(value, '$name:');
    }

    super.visitNamedExpression(node);
  }

  /// SIMF-C4 — a user-visible string that never reached `AppL10n`.
  ///
  /// Deliberately conservative: single characters, digits and punctuation-only
  /// values (`':'`, `'—'`, `'%'`) are separators, not copy, and flagging them
  /// would bury the real findings.
  void _flagUserFacingString(Expression expression, String context) {
    // `isArabic ? 'العربية' : 'English'` is hand-rolled localization: both
    // branches are copy that belongs in AppL10n.
    if (expression is ConditionalExpression) {
      _flagUserFacingString(expression.thenExpression, context);
      _flagUserFacingString(expression.elseExpression, context);
      return;
    }
    if (expression is! SimpleStringLiteral) return;
    final String value = expression.value.trim();
    if (value.length < 2) return;
    if (!RegExp(r'[A-Za-z؀-ۿ]').hasMatch(value)) return;
    _add(
      rule: 'SIMF-C4',
      offset: expression.offset,
      message: "$context '$value'",
      remedy: Remedy.localization,
    );
  }

  // ── SIMF-C2 / C5 — path literals ────────────────────────────────────────
  @override
  void visitSimpleStringLiteral(SimpleStringLiteral node) {
    _flagPathLiteral(node.value, node.offset);
    super.visitSimpleStringLiteral(node);
  }

  @override
  void visitStringInterpolation(StringInterpolation node) {
    // `'$baseUrl/app/assets/NewsImage/${item.id}/image'` — the duplication the
    // shared asset-URL builder exists to kill.
    _flagPathLiteral(node.toSource(), node.offset);
    super.visitStringInterpolation(node);
  }

  void _flagPathLiteral(String raw, int offset) {
    if (raw.contains('/app/assets/')) {
      _add(
        rule: 'SIMF-C2',
        offset: offset,
        message: 'asset URL built inline: $raw',
        remedy: Remedy.assetUrls,
      );
      return;
    }
    if (raw.startsWith('/app/') || raw.contains("'/app/")) {
      _add(
        rule: 'SIMF-C2',
        offset: offset,
        message: 'endpoint path: $raw',
        remedy: Remedy.endpoints,
      );
      return;
    }
    if (raw.startsWith('assets/')) {
      _add(
        rule: 'SIMF-C5',
        offset: offset,
        message: 'bundled asset path: $raw',
        remedy: Remedy.appAssets,
      );
    }
  }

  // ── SIMF-C3 / C6 — declaration placement ────────────────────────────────
  @override
  void visitClassDeclaration(ClassDeclaration node) {
    final String name = node.name.lexeme;
    final String? superName = node.extendsClause?.superclass.name.lexeme;

    // A `_FooState extends State<Foo>` is REQUIRED by the framework — only the
    // widget classes themselves are placement violations.
    if (name.startsWith('_') && superName != null &&
        _widgetSupertypes.contains(superName)) {
      _add(
        rule: 'SIMF-C3',
        offset: node.offset,
        message: 'private widget $name extends $superName',
        remedy: Remedy.ownFile,
      );
    }

    if (_isRepositoryFile && _declaresJsonMapping(node)) {
      _add(
        rule: 'SIMF-C6',
        offset: node.offset,
        message: 'model $name declared inside a repository file',
        remedy: Remedy.modelFile,
      );
    }

    super.visitClassDeclaration(node);
  }

  /// A DTO, as opposed to the repository itself: it maps to or from JSON.
  bool _declaresJsonMapping(ClassDeclaration node) {
    for (final ClassMember member in node.members) {
      if (member is ConstructorDeclaration &&
          (member.name?.lexeme ?? '').startsWith('fromJson')) {
        return true;
      }
      if (member is MethodDeclaration &&
          (member.name.lexeme == 'toJson' ||
              member.name.lexeme.startsWith('fromJson'))) {
        return true;
      }
    }
    return false;
  }

  @override
  void visitMethodDeclaration(MethodDeclaration node) {
    final String name = node.name.lexeme;
    final String? returns = node.returnType?.toSource();
    if (name.startsWith('_build') &&
        returns != null &&
        (returns == 'Widget' ||
            returns == 'List<Widget>' ||
            returns == 'PreferredSizeWidget')) {
      _add(
        rule: 'SIMF-C3',
        offset: node.offset,
        message: 'widget-building method $name() returning $returns',
        remedy: Remedy.ownFile,
      );
    }
    super.visitMethodDeclaration(node);
  }
}
