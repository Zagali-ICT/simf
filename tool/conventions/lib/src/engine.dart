import 'dart:io';

import 'package:path/path.dart' as p;

import 'config.dart';
import 'dart_rules.dart';
import 'text_rules.dart';
import 'violation.dart';

/// Scans every configured surface under [repoRoot] and returns the findings,
/// ordered so two runs of the same tree produce identical output.
List<Violation> scanRepository(String repoRoot) {
  final List<Violation> found = <Violation>[
    ..._scanTree(repoRoot, Config.flutterLib),
    for (final String root in _packageLibRoots(repoRoot))
      ..._scanTree(repoRoot, root),
    for (final String root in Config.razorRoots) ..._scanTree(repoRoot, root),
  ];

  found.sort((Violation a, Violation b) {
    final int byRule = a.rule.compareTo(b.rule);
    if (byRule != 0) return byRule;
    final int byFile = a.file.compareTo(b.file);
    if (byFile != 0) return byFile;
    return a.line.compareTo(b.line);
  });
  return found;
}

/// Every `packages/*/lib` under [Config.flutterPackagesRoot], sorted so two
/// runs order their findings identically. Discovered rather than listed: a
/// hand-maintained list silently drops the next package added, which is the
/// failure this scan root exists to fix.
List<String> _packageLibRoots(String repoRoot) {
  final Directory packages =
      Directory(p.join(repoRoot, Config.flutterPackagesRoot));
  if (!packages.existsSync()) return const <String>[];

  final List<String> roots = <String>[];
  for (final FileSystemEntity entity in packages.listSync(followLinks: false)) {
    if (entity is! Directory) continue;
    final Directory lib = Directory(p.join(entity.path, 'lib'));
    if (lib.existsSync()) {
      roots.add(Config.relativePosix(lib.path, repoRoot));
    }
  }
  roots.sort();
  return roots;
}

List<Violation> _scanTree(String repoRoot, String relativeRoot) {
  final Directory dir = Directory(p.join(repoRoot, relativeRoot));
  if (!dir.existsSync()) return const <Violation>[];

  final List<Violation> found = <Violation>[];
  for (final FileSystemEntity entity
      in dir.listSync(recursive: true, followLinks: false)) {
    if (entity is! File) continue;

    final String posixPath = Config.relativePosix(entity.path, repoRoot);
    if (Config.isExcluded(posixPath)) continue;

    final String extension = p.extension(posixPath).toLowerCase();
    if (extension == '.dart') {
      if (Config.isTest(posixPath)) continue;
      found.addAll(
        analyseDartFile(
          posixPath: posixPath,
          content: entity.readAsStringSync(),
        ),
      );
    } else if (extension == '.razor') {
      found.addAll(
        analyseRazorFile(
          posixPath: posixPath,
          content: entity.readAsStringSync(),
        ),
      );
    } else if (extension == '.css') {
      found.addAll(
        analyseCssFile(
          posixPath: posixPath,
          content: entity.readAsStringSync(),
        ),
      );
    }
  }
  return found;
}

/// Findings whose fingerprint is absent from [baseline].
///
/// This is the gate: pre-existing debt is tolerated while the waves land, but a
/// newly introduced violation fails the build on the commit that introduced it.
List<Violation> newSince(List<Violation> found, Set<String> baseline) =>
    found.where((Violation v) => !baseline.contains(v.fingerprint)).toList();
