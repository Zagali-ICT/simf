import 'dart:io';

import 'package:path/path.dart' as p;

/// Walks up from [start] until it finds the repository root.
///
/// The marker is a `.git` entry of EITHER kind. In an ordinary clone `.git` is
/// a directory; in a **git worktree** it is a small FILE holding a `gitdir:`
/// pointer. Testing only for a directory made this walk fail in every worktree,
/// fall through to the fallback below, and scan a root with no sources under
/// it — so the checker printed "No violations found" and the CI gate passed
/// vacuously, which is the one failure mode a gate must not have. Found while
/// running the clean-code programme from a worktree, 2026-08-14.
///
/// Returns [fallback] (the process's working directory in production) when no
/// marker is found, which is why a silent wrong answer was possible at all.
String inferRepoRoot(Directory start, {required String fallback}) {
  Directory dir = start;
  while (dir.path != dir.parent.path) {
    if (isRepoRoot(dir.path)) {
      return dir.path;
    }
    dir = dir.parent;
  }
  return fallback;
}

/// Whether [dirPath] carries a `.git` marker, as a directory or as a worktree's
/// pointer file.
bool isRepoRoot(String dirPath) {
  final String marker = p.join(dirPath, '.git');
  return Directory(marker).existsSync() || File(marker).existsSync();
}
