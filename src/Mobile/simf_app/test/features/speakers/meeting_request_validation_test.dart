import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/speakers/meeting_request_validation.dart';

const _en = AppL10n(Locale('en'));
const _noTarget = 'Select a speaker first';

String? _error({
  bool hasTarget = true,
  String subject = 'Naval cooperation',
  String? Function()? validateExtra,
  bool hasSlot = true,
}) =>
    meetingRequestError(
      l10n: _en,
      hasTarget: hasTarget,
      noTargetSelectedError: _noTarget,
      subject: subject,
      validateExtra: validateExtra,
      hasSlot: hasSlot,
    );

void main() {
  group('meetingRequestError', () {
    test('a complete form has nothing to say', () {
      expect(_error(), isNull);
    });

    test('reports the first unmet precondition, in the order the sheet '
        'presents its fields', () {
      expect(_error(hasTarget: false, subject: '', hasSlot: false), _noTarget);
      expect(_error(subject: '', hasSlot: false), _en.meetingRequestInvalid);
      // G3 — a slot is always required (picked a day but not a time).
      expect(_error(hasSlot: false), _en.meetingPickDateTime);
    });

    test('the extra field is validated between the subject and the slot', () {
      expect(_error(validateExtra: () => 'Enter attendees'), 'Enter attendees');
      expect(
        _error(validateExtra: () => 'Enter attendees', hasSlot: false),
        'Enter attendees',
      );
      expect(
        _error(subject: '', validateExtra: () => 'Enter attendees'),
        _en.meetingRequestInvalid,
      );
    });

    test('the extra validator is not run until the checks before it pass', () {
      // It reads live controller text, so an early run complains first about
      // a field the user has not reached.
      var calls = 0;
      String? countingExtra() {
        calls++;
        return 'Enter attendees';
      }

      expect(
        _error(subject: '', validateExtra: countingExtra),
        _en.meetingRequestInvalid,
      );
      expect(calls, 0);
    });
  });
}
