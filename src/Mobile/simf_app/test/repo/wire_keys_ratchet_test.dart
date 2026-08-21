import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

// The wire-key ratchet.
//
// Every `fromJson` in this app is TOLERANT by design:
// `json['nameArabic'] as String? ?? ''`, enum decoders that fall back to a
// default for anything they do not recognise. That is the right behaviour at
// runtime — a partial payload must not crash the app — but it means a renamed
// or dropped JSON key does NOT throw. It decodes to the fallback, silently,
// and every existing test stays green while the app shows empty strings.
//
// So the contract cannot be defended by the unit tests. It is defended here,
// statically: the set of JSON key literals the app reads and writes is pinned
// to `wire_keys.snapshot`.
//
// WHY THIS MATTERS: the deployed app decodes these keys against the live API,
// and against JSON already persisted on users' devices (the offline gate
// queue, the badge config). A rename breaks shipped installs — they cannot be
// repaired by redeploying the backend alone.
//
// HOW TO TREAT A FAILURE
//
// The snapshot was generated ONCE from the pre-refactor tree. It is NEVER
// regenerated to make a build green. There is deliberately no
// `--update-snapshot` switch, and a missing snapshot is a hard failure rather
// than a silent re-create — either would turn this ratchet into a rubber
// stamp the first time it was inconvenient.
//
//   * A key REMOVED from the code is a wire-contract BREAK. Restore the key.
//   * A key ADDED is fine when it is a genuinely new endpoint or field;
//     append it to the snapshot in the same changeset.
//   * A RENAME appears as one ADDED + one REMOVED together. That is the
//     signature to look for — it is what a `freezed` conversion gets wrong —
//     and it needs an owner decision, not a snapshot edit.
//
// WHAT THIS RATCHET DOES **NOT** CATCH — read this before trusting it.
//
// It pins the SET of key literals the code mentions. It does not know which
// decoder reads which key, so swapping one read for a DIFFERENT key that is
// already in the set leaves the set unchanged and the ratchet green. The
// 2026-08-21 mutation sweep proved it: `gate_models.dart` reading
// `json['name']` where it should read `json['nameArabic']` survived this test
// untouched, because both names are already in the snapshot from elsewhere in
// the app. Sibling-key swaps are exactly the shape this ratchet is blind to,
// and they are common — every bilingual model carries `x` beside `xArabic`,
// and the app is full of `name` / `code` / `id` repeated across features.
//
// A tolerant decoder makes that silent twice over: the wrong key is usually
// absent, so it falls back rather than throwing, and the fallback then drives
// whatever the field drives. Where a fallback decides a PERMISSION rather than
// a label, that is a gate opening on its own.
//
// So this file is the RENAME defence, not the correctness defence. What a key
// decodes TO is defended per-decoder, by tests that feed a sentinel fixture
// (a value no fallback can produce, proving the key is read) paired with a
// key-absent fixture (pinning the fallback itself) — see `test/wire/` for the
// device-persisted blobs, and the fallback tests sitting beside each feature's
// models for the server responses. Do NOT read a green ratchet as meaning a
// decoder reads the right key.
//
// The working directory for `flutter test` is the package root
// (`src/Mobile/simf_app`), so every path below is relative to that. That is
// deliberate: walking up the tree looking for a `.git` DIRECTORY finds the
// WRONG tree in a git worktree, where `.git` is a FILE. The test would then
// scan somewhere else entirely and pass vacuously.

/// The committed set of wire keys. One key per line.
const String _snapshotPath = 'test/repo/wire_keys.snapshot';

/// Decode side: `json['someKey']`, plus the `map` / `data` / `m` receivers the
/// repositories also use. The receiver name is not captured — only the key.
final RegExp _decodeKey = RegExp(
  r"[A-Za-z_$][A-Za-z0-9_$]*\['([A-Za-z_][A-Za-z0-9_]*)'\]",
);

/// Encode side: `'someKey':` inside a `Map<String, dynamic>` literal or a
/// `toJson` body. The colon must follow the quote immediately — `'ar' : 'en'`
/// with a space is a ternary, not a key, and that alone excludes it.
final RegExp _encodeKey = RegExp("'([A-Za-z_][A-Za-z0-9_]*)':");

/// Windows `Directory.listSync` returns backslash paths; normalise so the
/// comparisons below read the same on every platform.
String _posix(String path) => path.replaceAll(r'\', '/');

/// The roots that hold wire-contract code: each feature's `data/` layer, the
/// cross-feature `core/`, and both local packages.
List<Directory> _scanRoots() {
  final roots = <Directory>[Directory('lib/core')];

  for (final feature in Directory('lib/features').listSync()) {
    if (feature is! Directory) {
      continue;
    }
    final data = Directory('${_posix(feature.path)}/data');
    if (data.existsSync()) {
      roots.add(data);
    }
  }

  for (final package in Directory('packages').listSync()) {
    if (package is! Directory) {
      continue;
    }
    final packageLib = Directory('${_posix(package.path)}/lib');
    if (packageLib.existsSync()) {
      roots.add(packageLib);
    }
  }

  return roots;
}

List<File> _scannedFiles() => _scanRoots()
    .expand((root) => root.listSync(recursive: true))
    .whereType<File>()
    .where((f) => f.path.endsWith('.dart'))
    .toList();

/// Every JSON key literal the app reads or writes, key names ONLY.
///
/// The cast and the default are deliberately NOT captured: `as String? ?? ''`
/// and `@JsonKey(defaultValue: '')` are different text for identical
/// behaviour, so capturing them would make a `freezed` conversion look like a
/// contract change. A ratchet that cries wolf gets deleted.
Set<String> _scanWireKeys() {
  final keys = <String>{};

  for (final file in _scannedFiles()) {
    for (final line in file.readAsLinesSync()) {
      final code = line.trimLeft();

      // Doc comments NAME keys in prose (`{ "items": [ … ] }`), and switch
      // labels match the key shape (`case 'error':`). Neither is a key.
      if (code.startsWith('//') || code.startsWith('case ')) {
        continue;
      }

      for (final match in _decodeKey.allMatches(line)) {
        keys.add(match.group(1)!);
      }
      for (final match in _encodeKey.allMatches(line)) {
        keys.add(match.group(1)!);
      }
    }
  }

  return keys;
}

Set<String> _readSnapshot() {
  final file = File(_snapshotPath);
  if (!file.existsSync()) {
    fail(
      '$_snapshotPath is missing. It is NOT regenerated automatically — that '
      'would let anyone turn this ratchet green by deleting it. Restore the '
      'committed file from source control.',
    );
  }
  return file
      .readAsLinesSync()
      .map((line) => line.trim())
      .where((line) => line.isNotEmpty)
      .toSet();
}

void main() {
  group('wire-key ratchet', () {
    test('the scan is not vacuous', () {
      // A wrong working directory, or a `.git`-walk that landed on another
      // tree, shows up here as an empty scan rather than as a green build.
      final files = _scannedFiles();
      expect(
        files.length,
        greaterThan(100),
        reason: 'Only ${files.length} Dart files were scanned. The wire roots '
            'are read relative to the package root, so this means the test '
            'ran somewhere unexpected — it is passing over nothing.',
      );

      expect(
        _scanWireKeys().length,
        greaterThan(300),
        reason: 'The scan found almost no JSON keys. Treat this as a broken '
            'scan, not as a clean tree.',
      );
    });

    test('every JSON key still matches the committed snapshot', () {
      final live = _scanWireKeys();
      final snapshot = _readSnapshot();

      final removed = snapshot.difference(live).toList()..sort();
      final added = live.difference(snapshot).toList()..sort();

      final removedList = removed.isEmpty
          ? '  (none)'
          : removed.map((k) => '  - $k').join('\n');
      final addedList =
          added.isEmpty ? '  (none)' : added.map((k) => '  + $k').join('\n');

      expect(
        removed.isEmpty && added.isEmpty,
        isTrue,
        reason: 'The set of JSON keys this app reads/writes has changed.\n'
            '\n'
            'REMOVED from the code (${removed.length}) — THIS IS THE '
            'DANGEROUS DIRECTION.\n'
            'The shipped app decodes these keys from the live API and from '
            "JSON already persisted on users' devices. Because every fromJson "
            'is tolerant, a key that disappears does not throw — it silently '
            "decodes to '' / null / a default enum, so the screen renders "
            'blank instead of failing. Restore the key.\n'
            '$removedList\n'
            '\n'
            'ADDED to the code (${added.length}).\n'
            'Fine when this is a genuinely new endpoint or field: append it '
            'to $_snapshotPath in the same changeset. NOT fine as a way to '
            'absorb a rename.\n'
            '$addedList\n'
            '\n'
            'A key in BOTH lists is a RENAME. That is a wire-contract change '
            'and needs an owner decision — do not edit the snapshot to match '
            'the code.',
      );
    });
  });
}
