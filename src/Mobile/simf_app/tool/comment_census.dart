// Comment census — measures the comment surface under `lib/`, and how much of
// it carries a design signal rather than restating the next line.
//
// Why this exists. Every other gate in this repo is blind to comments:
// `tool/conventions`, the `test/repo/` ratchets and the golden harness all
// read code and pixels, so a sweep that deletes documentation leaves every
// gate green. The one thing a comment sweep must never do is delete the
// comments that record a decision, a Figma node, a bug or a backend contract
// — CLAUDE.md section 0 states those stay — and until now nothing measured
// them.
//
// Run it:
//
//     dart run tool/comment_census.dart        # from the package root
//
// It prints one JSON object: total comment lines, total comment blocks, and
// the number of blocks carrying a signal. `test/repo/
// comment_signal_ratchet_test.dart` calls the same functions and ratchets the
// last number, so the census and the gate can never measure different things.
//
// Only the signal count is a gate. The two totals are reported and deliberately
// NOT pinned: making them fall is the whole point of the sweep.

import 'dart:convert';
import 'dart:io';

/// The three numbers the census reports.
class CommentCensus {
  const CommentCensus({
    required this.commentLines,
    required this.commentBlocks,
    required this.signalBlocks,
  });

  /// Every line whose first non-space characters are `//`.
  final int commentLines;

  /// Maximal runs of consecutive comment lines. A blank line or a line of
  /// code ends a run, so one run is one thought.
  final int commentBlocks;

  /// Blocks matching [blockCarriesSignal].
  final int signalBlocks;

  Map<String, int> toJson() => <String, int>{
        'commentLines': commentLines,
        'commentBlocks': commentBlocks,
        'signalBlocks': signalBlocks,
      };
}

/// A decision id — `D-219`, `D-771`. The boundary keeps `FDS-014` and
/// `MOD-001` from matching here; both are signal anyway, via `_signalTokens`.
final RegExp _decisionId = RegExp(r'\bD-\d+\b');

/// A bug id, in the three forms this programme has used.
final RegExp _bugId = RegExp(r'\b(?:BUG|DEF|BF)-\d+\b');

/// A Figma node id — `758:1134`, `922-2824`, `1426-10771`.
///
/// Three digits a side, NOT the bare `\d+[:-]\d+` a loose reading suggests.
/// The loose form also matches the date in `2026-08-13`, which would mark
/// every dated note as protected — including dated notes that are pure noise.
/// The sweep would then trip this gate while doing exactly what it was asked
/// to do, and a gate that cries wolf gets slackened or deleted. Every node in
/// `docs/pages/FIGMA-NODE-MAP.md` clears three digits a side, and a block
/// citing a shorter one says "Figma" beside it, which is a signal token.
final RegExp _figmaNodeId = RegExp(r'\b\d{3,}[:-]\d{3,}\b');

/// Arabic. The app is RTL-first and the comments quote real UI copy.
final RegExp _arabic = RegExp('[\u0600-\u06FF]');

/// Case-sensitive prefixes: a controlled-document id or a requirement id.
const List<String> _signalTokens = <String>['SIMF-', 'FR-'];

/// Matched case-insensitively as substrings, so `supersede` also catches
/// `superseded` and `supersedes`.
const List<String> _signalWords = <String>[
  'figma',
  'node',
  'wire',
  'contract',
  'owner',
  'supersede',
];

/// Whether [block] records something a reader could not recover from the code.
bool blockCarriesSignal(List<String> block) {
  final text = block.join('\n');
  if (_decisionId.hasMatch(text) ||
      _bugId.hasMatch(text) ||
      _figmaNodeId.hasMatch(text) ||
      _arabic.hasMatch(text)) {
    return true;
  }
  if (_signalTokens.any(text.contains)) {
    return true;
  }
  final lowered = text.toLowerCase();
  return _signalWords.any(lowered.contains);
}

/// Every Dart file under [root], in a stable order.
List<File> dartFilesUnder(Directory root) {
  final files = root
      .listSync(recursive: true)
      .whereType<File>()
      .where((file) => file.path.endsWith('.dart'))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));
  return files;
}

/// Counts the comment surface of every Dart file under [root].
///
/// A comment line is one whose first non-space characters are `//`, which
/// covers `//`, `///` and `////`. Trailing comments after code are not
/// counted: `lib/` holds no `/* */` block comment, so this reading needs no
/// lexer and cannot mistake a `//` inside a string literal for a comment —
/// the same trade the `test/repo/design_token_ratchet_test.dart` scan makes.
CommentCensus censusOf(Directory root) {
  var commentLines = 0;
  var commentBlocks = 0;
  var signalBlocks = 0;
  var block = <String>[];

  void closeBlock() {
    if (block.isEmpty) {
      return;
    }
    commentBlocks++;
    if (blockCarriesSignal(block)) {
      signalBlocks++;
    }
    block = <String>[];
  }

  for (final file in dartFilesUnder(root)) {
    for (final line in file.readAsLinesSync()) {
      if (line.trimLeft().startsWith('//')) {
        block.add(line);
        commentLines++;
        continue;
      }
      closeBlock();
    }
    closeBlock();
  }

  return CommentCensus(
    commentLines: commentLines,
    commentBlocks: commentBlocks,
    signalBlocks: signalBlocks,
  );
}

void main(List<String> args) {
  final root = Directory(args.isEmpty ? 'lib' : args.first);
  if (!root.existsSync()) {
    stderr.writeln('comment_census: no such directory: ${root.path}');
    exitCode = 2;
    return;
  }
  stdout.writeln(jsonEncode(censusOf(root)));
}
