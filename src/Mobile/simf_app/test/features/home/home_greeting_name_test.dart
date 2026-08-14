// The visitor greeting shortens to the first TWO words; everyone else keeps
// their full name. Two rather than one is the whole point: the single-token
// rule shipped on 2026-07-21 and was reverted under OA-D1 because it split
// Arabic compound given names, so those cases are pinned here rather than
// left to a reviewer to remember.
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/home/home_greeting.dart';

void main() {
  group('greetingDisplayName for a visitor', () {
    test('a two-word name is already short enough to keep whole', () {
      expect(
        greetingDisplayName('Ahmed Al-Subaie', isVisitor: true),
        'Ahmed Al-Subaie',
      );
    });

    test('a long English name keeps the first two words', () {
      expect(
        greetingDisplayName('Ahmed Ali Mohammed Al-Subaie', isVisitor: true),
        'Ahmed Ali',
      );
    });

    test('عبد الله survives intact, which the single-token rule broke', () {
      expect(greetingDisplayName('عبد الله', isVisitor: true), 'عبد الله');
    });

    test('عبد الرحمن survives intact for the same reason', () {
      expect(greetingDisplayName('عبد الرحمن', isVisitor: true), 'عبد الرحمن');
    });

    test('a compound given name plus a family name keeps the compound', () {
      expect(
        greetingDisplayName('عبد الله السبيعي', isVisitor: true),
        'عبد الله',
      );
    });

    test('a three-word Arabic name keeps the first two', () {
      expect(
        greetingDisplayName('محمد أحمد السبيعي', isVisitor: true),
        'محمد أحمد',
      );
    });

    test('a single word is returned unchanged', () {
      expect(greetingDisplayName('Ahmed', isVisitor: true), 'Ahmed');
    });

    test('runs of whitespace do not produce empty words', () {
      expect(
        greetingDisplayName('  Ahmed    Ali   Mohammed  ', isVisitor: true),
        'Ahmed Ali',
      );
    });
  });

  group('greetingDisplayName for everyone else', () {
    test('a partner keeps the whole name, however long', () {
      const name = 'Maritime News Network International';
      expect(greetingDisplayName(name, isVisitor: false), name);
    });

    test('an Arabic organisation name is not clipped', () {
      const name = 'شبكة الأخبار البحرية الدولية';
      expect(greetingDisplayName(name, isVisitor: false), name);
    });
  });

  group('greetingDisplayName edge cases', () {
    test('an empty name stays empty for a visitor', () {
      expect(greetingDisplayName('', isVisitor: true), '');
    });

    test('a whitespace-only name collapses to empty', () {
      expect(greetingDisplayName('   ', isVisitor: true), '');
      expect(greetingDisplayName('   ', isVisitor: false), '');
    });

    test('surrounding whitespace is trimmed for a non-visitor too', () {
      expect(
        greetingDisplayName('  Sponsor Co  ', isVisitor: false),
        'Sponsor Co',
      );
    });
  });
}
