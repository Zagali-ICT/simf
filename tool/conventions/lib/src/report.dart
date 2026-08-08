import 'dart:convert';

import 'violation.dart';

/// Renders findings in the SAME shape as the external review that prompted this
/// tool: grouped by feature, then `Issue file / Issue / Fix` per finding.
///
/// The format is deliberate. The reviewer's document and this tool's output can
/// be laid side by side and compared line for line, which is what lets the team
/// run the review itself before delivery instead of receiving it afterwards.
String renderMarkdown(List<Violation> violations, {required String generatedAt}) {
  final StringBuffer out = StringBuffer()
    ..writeln('# SIMF convention report')
    ..writeln()
    ..writeln('Generated $generatedAt by `dart run tool/conventions`.')
    ..writeln();

  if (violations.isEmpty) {
    out
      ..writeln('**No violations found.**')
      ..writeln()
      ..writeln(
        'Every rule in SIMF-CQP-001 section 6 passed. This is the state the '
        'programme exists to hold.',
      );
    return out.toString();
  }

  out
    ..writeln('## Summary')
    ..writeln()
    ..writeln('| Rule | Findings |')
    ..writeln('|------|----------|');

  final Map<String, int> byRule = <String, int>{};
  for (final Violation v in violations) {
    byRule[v.rule] = (byRule[v.rule] ?? 0) + 1;
  }
  final List<String> rules = byRule.keys.toList()..sort();
  for (final String rule in rules) {
    out.writeln('| $rule | ${byRule[rule]} |');
  }
  out
    ..writeln('| **Total** | **${violations.length}** |')
    ..writeln();

  final Map<String, List<Violation>> byFeature = <String, List<Violation>>{};
  for (final Violation v in violations) {
    byFeature.putIfAbsent(v.feature, () => <Violation>[]).add(v);
  }
  final List<String> features = byFeature.keys.toList()..sort();

  for (final String feature in features) {
    final List<Violation> items = byFeature[feature]!
      ..sort((Violation a, Violation b) {
        final int byFile = a.file.compareTo(b.file);
        return byFile != 0 ? byFile : a.line.compareTo(b.line);
      });

    out
      ..writeln('## $feature feature')
      ..writeln();

    String? currentFile;
    for (final Violation v in items) {
      if (v.file != currentFile) {
        currentFile = v.file;
        out
          ..writeln()
          ..writeln('Issue file : ${v.file}');
      }
      out
        ..writeln('Issue : ${v.message}  (line ${v.line}, ${v.rule})')
        ..writeln('Fix : ${v.remedy}');
    }
    out.writeln();
  }

  return out.toString();
}

/// The baseline: the set of fingerprints tolerated today.
///
/// Stored sorted so a regenerated baseline produces a reviewable diff rather
/// than a reordered file.
String renderBaseline(List<Violation> violations) {
  final List<String> fingerprints =
      violations.map((Violation v) => v.fingerprint).toSet().toList()..sort();
  return '${const JsonEncoder.withIndent('  ').convert(<String, Object?>{
        'version': 1,
        'note':
            'Fingerprints tolerated at the time of recording. The gate fails on '
                'anything NOT in this list. Entries are removed as waves land; '
                'none are ever added by hand.',
        'fingerprints': fingerprints,
      })}\n';
}

Set<String> parseBaseline(String json) {
  final Object? decoded = jsonDecode(json);
  if (decoded is! Map<String, Object?>) return <String>{};
  final Object? list = decoded['fingerprints'];
  if (list is! List) return <String>{};
  return list.whereType<String>().toSet();
}
