import 'package:flutter_test/flutter_test.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

void main() {
  group('AppRole', () {
    test('wire values match the confirmed enum (Guest=0..Staff=3)', () {
      expect(AppRole.guest.wireValue, equals(0));
      expect(AppRole.visitor.wireValue, equals(1));
      expect(AppRole.moderator.wireValue, equals(2));
      expect(AppRole.staff.wireValue, equals(3));
    });

    test('wire names match the backend casing', () {
      expect(AppRole.guest.wireName, equals('Guest'));
      expect(AppRole.visitor.wireName, equals('Visitor'));
      expect(AppRole.moderator.wireName, equals('Moderator'));
      expect(AppRole.staff.wireName, equals('Staff'));
    });

    test('fromJson reads the string form', () {
      expect(AppRole.fromJson('Visitor'), equals(AppRole.visitor));
      expect(AppRole.fromJson('Staff'), equals(AppRole.staff));
      expect(AppRole.fromJson('Moderator'), equals(AppRole.moderator));
    });

    test('fromJson reads the int form', () {
      expect(AppRole.fromJson(0), equals(AppRole.guest));
      expect(AppRole.fromJson(3), equals(AppRole.staff));
    });

    test('fromJson falls back to Guest for unknown / null / wrong types', () {
      expect(AppRole.fromJson(null), equals(AppRole.guest));
      expect(AppRole.fromJson('Unknown'), equals(AppRole.guest));
      expect(AppRole.fromJson(99), equals(AppRole.guest));
      expect(AppRole.fromJson(<String, dynamic>{}), equals(AppRole.guest));
    });

    test('isAtLeast respects the precedence guest < visitor < mod < staff',
        () {
      expect(AppRole.staff.isAtLeast(AppRole.visitor), isTrue);
      expect(AppRole.moderator.isAtLeast(AppRole.visitor), isTrue);
      expect(AppRole.visitor.isAtLeast(AppRole.visitor), isTrue);
      expect(AppRole.guest.isAtLeast(AppRole.visitor), isFalse);
      expect(AppRole.visitor.isAtLeast(AppRole.staff), isFalse);
    });
  });
}
