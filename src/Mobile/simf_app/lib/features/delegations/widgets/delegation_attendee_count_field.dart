import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/widgets/meeting_sheet_fields.dart';

/// The عدد الحضور (attendee count) input on the delegation meeting-request
/// sheet — the sheet's beige field, digits only, capped at four digits.
class DelegationAttendeeCountField extends StatelessWidget {
  const DelegationAttendeeCountField({
    required this.controller,
    required this.hintText,
    super.key,
  });

  final TextEditingController controller;
  final String hintText;

  @override
  Widget build(BuildContext context) => TextField(
        key: const ValueKey<String>('delegation-attendees'),
        controller: controller,
        keyboardType: TextInputType.number,
        inputFormatters: <TextInputFormatter>[
          FilteringTextInputFormatter.digitsOnly,
          LengthLimitingTextInputFormatter(4),
        ],
        style: SimfTokens.bodyInputMd,
        decoration: meetingSheetInputDecoration(
          hintText: hintText,
          border: const OutlineInputBorder(),
        ),
      );
}
