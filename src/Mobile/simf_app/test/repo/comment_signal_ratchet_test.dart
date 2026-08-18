import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

// The repo holds zero relative imports and `always_use_package_imports` is at
// `error`, so this one looks like a breach and is not: `tool/` sits outside
// `lib/`, has no `package:simf_app/` URI, and the analyzer confirms the rule
// does not fire here. The alternative is a second copy of the counting logic
// in this file, and a gate that measures something slightly different from the
// census it quotes is worse than an unusual import.
import '../../tool/comment_census.dart';

/// Pins the comment surface's SIGNAL, because nothing else in this repo can
/// see a comment at all.
///
/// `tool/conventions`, the other `test/repo/` ratchets and the 63 committed
/// goldens read code and pixels. A sweep that deletes documentation therefore
/// leaves every gate green — it is the one change in the clean-code programme
/// that is invisible to the build. CLAUDE.md section 0 says the comments that
/// record a decision id, a Figma node or a backend contract STAY; that
/// sentence was the only thing enforcing it.
///
/// So this counts them. A comment BLOCK is a maximal run of consecutive
/// comment lines — a blank line or a line of code ends it, so one block is one
/// thought. A block carries signal when it names a decision id (`D-219`), a
/// bug id (`BUG-012`), a Figma node (`758:1134`), Arabic UI copy, or one of
/// the words that mark an external contract (Figma / node / wire / contract /
/// owner / supersede / `SIMF-` / `FR-`). The definitions live in
/// `tool/comment_census.dart` and are imported, not restated, so the number
/// below and the number `dart run tool/comment_census.dart` prints are the
/// same measurement.
///
/// **Only the signal count is ratcheted.** Total comment lines and total
/// blocks are reported by the census and deliberately left unpinned: making
/// those fall is what the sweep is FOR, and a gate that forbids it would fail
/// the programme it is meant to protect.
///
/// The working directory for `flutter test` is the package root
/// (`src/Mobile/simf_app`), so the path below is relative to that.

/// Measured 2026-08-17 on the clean-code worktree, while the comment sweep was
/// mid-flight: 11,539 comment lines in 3,444 blocks, of which 1,789 carry
/// signal.
///
/// A drop means a block recording a decision, a node, a contract or Arabic
/// copy was deleted. Restore it. Lowering this constant is not a fix — if a
/// signal block genuinely had to go (a decision that was reversed, a node that
/// no longer exists), say which one and why in the same changeset, then move
/// the number by exactly that many.
const int _signalBlockBaseline = 1789;

void main() {
  group('comment signal ratchet', () {
    test('lib/ still carries at least $_signalBlockBaseline signal blocks', () {
      final census = censusOf(Directory('lib'));

      expect(
        census.signalBlocks,
        greaterThanOrEqualTo(_signalBlockBaseline),
        reason: 'The comment sweep deleted documentation, not noise. '
            '${census.signalBlocks} blocks under lib/ carry a decision id, a '
            'bug id, a Figma node, Arabic copy or a contract word — the '
            'baseline is $_signalBlockBaseline. Run '
            '`dart run tool/comment_census.dart` and restore what went '
            'missing; CLAUDE.md section 0 keeps these comments.',
      );
    });

    test('the census still sees the comment surface it is scanning', () {
      final census = censusOf(Directory('lib'));

      // A guard on the guard. If the scan silently stopped matching — a
      // changed marker, a directory walk that found nothing — signalBlocks
      // would read 0 and the ratchet above would be the only thing failing,
      // which reads as "documentation was deleted" and sends the next reader
      // hunting through a diff that never touched a comment.
      expect(
        census.commentBlocks,
        greaterThan(census.signalBlocks),
        reason: 'Every comment block scanned as signal-bearing, which means '
            'the signal test is matching everything rather than measuring '
            'anything. Check tool/comment_census.dart.',
      );
    });
  });
}
