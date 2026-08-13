import 'dart:io';

import 'package:path/path.dart' as p;
import 'package:simf_conventions/src/engine.dart';
import 'package:simf_conventions/src/report.dart';
import 'package:simf_conventions/src/violation.dart';

const String _usage = '''
SIMF convention checker (SIMF-CQP-001).

  dart run tool/conventions                 scan and print the report
  dart run tool/conventions --out FILE      write the report to FILE
  dart run tool/conventions --write-baseline
                                            record today's findings as tolerated
  dart run tool/conventions --check         fail if any finding is NOT in the
                                            baseline (this is the CI gate)
  dart run tool/conventions --check --strict
                                            fail if there is ANY finding at all
                                            (the Wave 6 end state)

Options:
  --root DIR        repository root (default: inferred from this script)
  --baseline FILE   baseline location (default: tool/conventions/baseline.json)
''';

Future<void> main(List<String> args) async {
  if (args.contains('--help') || args.contains('-h')) {
    stdout.write(_usage);
    return;
  }

  final String repoRoot = _option(args, '--root') ?? _inferRepoRoot();
  final String baselinePath =
      _option(args, '--baseline') ?? p.join(repoRoot, 'tool', 'conventions', 'baseline.json');

  final List<Violation> found = scanRepository(repoRoot);

  if (args.contains('--write-baseline')) {
    File(baselinePath)
      ..createSync(recursive: true)
      ..writeAsStringSync(renderBaseline(found));
    stdout.writeln(
      'Baseline recorded: ${found.length} findings tolerated -> $baselinePath',
    );
    return;
  }

  final String markdown =
      renderMarkdown(found, generatedAt: DateTime.now().toIso8601String().split('T').first);

  final String? outPath = _option(args, '--out');
  if (outPath != null) {
    File(outPath)
      ..createSync(recursive: true)
      ..writeAsStringSync(markdown);
    stdout.writeln('Report written to $outPath (${found.length} findings).');
  } else {
    stdout.write(markdown);
  }

  if (!args.contains('--check')) return;

  if (args.contains('--strict')) {
    if (found.isEmpty) {
      stdout.writeln('\nPASS: zero convention violations.');
      return;
    }
    stderr.writeln('\nFAIL: ${found.length} convention violations (strict mode).');
    exitCode = 1;
    return;
  }

  final File baselineFile = File(baselinePath);
  if (!baselineFile.existsSync()) {
    stderr.writeln(
      '\nFAIL: no baseline at $baselinePath. Run --write-baseline first.',
    );
    exitCode = 1;
    return;
  }

  final List<Violation> fresh =
      newSince(found, parseBaseline(baselineFile.readAsStringSync()));
  if (fresh.isEmpty) {
    stdout.writeln(
      '\nPASS: no NEW convention violations (${found.length} pre-existing, tracked in the baseline).',
    );
    return;
  }

  stderr.writeln('\nFAIL: ${fresh.length} NEW convention violation(s):\n');
  for (final Violation v in fresh) {
    stderr.writeln('  ${v.rule}  ${v.file}:${v.line}');
    stderr.writeln('        ${v.message}');
    stderr.writeln('        -> ${v.remedy}');
  }
  stderr.writeln(
    '\nSee docs/SIMF-CQP-001-Code-Quality-Programme.md section 5 for where each '
    'literal belongs.',
  );
  exitCode = 1;
}

String? _option(List<String> args, String name) {
  final int index = args.indexOf(name);
  if (index == -1 || index + 1 >= args.length) return null;
  return args[index + 1];
}

/// Walks up from this script until it finds the repository root.
String _inferRepoRoot() {
  Directory dir = File.fromUri(Platform.script).parent;
  while (dir.path != dir.parent.path) {
    if (Directory(p.join(dir.path, '.git')).existsSync()) return dir.path;
    dir = dir.parent;
  }
  return Directory.current.path;
}
