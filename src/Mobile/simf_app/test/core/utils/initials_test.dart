import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/initials.dart';

/// Two rules, kept apart on purpose. These tests exist mostly to pin the
/// DIFFERENCE: an audit read them as one duplicated helper, and merging them
/// would have changed what several tiles render.
void main() {
  group('initialsFromStart', () {
    test('takes the first two characters', () {
      expect(initialsFromStart('Naval Command'), 'NA');
    });

    test('takes one when the name is a single character', () {
      expect(initialsFromStart('N'), 'N');
    });

    test('is empty for a blank name', () {
      expect(initialsFromStart(''), '');
      expect(initialsFromStart('   '), '');
    });

    test('trims and uppercases', () {
      expect(initialsFromStart('  naval  '), 'NA');
    });
  });

  group('initialsFromWords', () {
    test('takes the first letter of the first two words', () {
      expect(initialsFromWords('Naval Command'), 'NC');
    });

    test('ignores words past the second', () {
      expect(initialsFromWords('Royal Saudi Naval Forces'), 'RS');
    });

    test('collapses runs of whitespace', () {
      expect(initialsFromWords('Naval    Command'), 'NC');
    });

    test('falls back to an em dash, never to an empty tile', () {
      expect(initialsFromWords(''), '—');
      expect(initialsFromWords('   '), '—');
    });

    test('handles a non-Latin name one grapheme at a time', () {
      // ا from القوات, ا from البحرية — uppercasing is a no-op in Arabic.
      expect(initialsFromWords('القوات البحرية'), 'اا');
    });
  });

  test('the two rules genuinely differ, which is why both exist', () {
    expect(initialsFromStart('Naval Command'), 'NA');
    expect(initialsFromWords('Naval Command'), 'NC');
  });
}
