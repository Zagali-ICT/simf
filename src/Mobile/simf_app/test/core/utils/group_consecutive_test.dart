import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/group_consecutive.dart';

/// The notifications list groups by RUN, not by bucket, and that distinction is
/// the whole reason this helper exists — so it is what these tests pin.
void main() {
  group('groupConsecutive', () {
    test('collects a run under one key', () {
      final groups = groupConsecutive<String, int>(
        <String>['aa', 'ab', 'ba'],
        (s) => s.codeUnitAt(0),
      );
      expect(groups.map((g) => g.value), <List<String>>[
        <String>['aa', 'ab'],
        <String>['ba'],
      ]);
    });

    test('does NOT merge two runs that share a key', () {
      // groupBy would give {a: [a1, a2], b: [b1]}. That would move the second
      // 'a' up the list, which is wrong for a server-ordered feed.
      final groups = groupConsecutive<String, String>(
        <String>['a1', 'b1', 'a2'],
        (s) => s[0],
      );
      expect(groups.map((g) => g.key), <String>['a', 'b', 'a']);
      expect(groups.last.value, <String>['a2']);
    });

    test('preserves the input order within a run', () {
      final groups = groupConsecutive<int, bool>(
        <int>[2, 4, 6],
        (n) => n.isEven,
      );
      expect(groups.single.value, <int>[2, 4, 6]);
    });

    test('an empty input gives an empty result', () {
      expect(groupConsecutive<int, int>(const <int>[], (n) => n), isEmpty);
    });

    test('a single item gives one group', () {
      final groups = groupConsecutive<int, int>(<int>[7], (n) => n);
      expect(groups, hasLength(1));
      expect(groups.single.key, 7);
    });
  });
}
