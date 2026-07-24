import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';

void main() {
  group('simfFieldDecoration', () {
    test('lets a field error wrap to multiple lines instead of clipping at one '
        "(Flutter's default errorMaxLines == 1)", () {
      // Every SIMF input shares this decoration, so a multi-line validation
      // message (e.g. the password policy checklist) must show in full.
      final errorMaxLines = simfFieldDecoration().errorMaxLines;
      expect(errorMaxLines, isNotNull);
      expect(errorMaxLines, greaterThanOrEqualTo(6));
    });
  });
}
