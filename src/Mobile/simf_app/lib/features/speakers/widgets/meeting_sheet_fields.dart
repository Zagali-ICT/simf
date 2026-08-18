import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/field_limits.dart';

/// The beige meeting-request sheet's form atoms (Figma **1776:5036**), shared
/// by the speaker sheet and the delegation sheet: the field label, the muted
/// hint line, the load spinner, the picker's type-to-filter field, the subject
/// input, and the one input decoration behind all three fields.

/// A form field label — navy, 12px, at the inline start (right, RTL).
class MeetingFieldLabel extends StatelessWidget {
  const MeetingFieldLabel({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Text(text, style: SimfTokens.labelNavyMediumSm),
      );
}

/// A muted, inline-start hint line (no-slots notice / choose-a-date-first).
class MeetingFieldHint extends StatelessWidget {
  const MeetingFieldHint({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: SimfTokens.space1),
          child: Text(text, style: SimfTokens.bodyGreySm),
        ),
      );
}

/// The small gold spinner the sheet shows while the picker list or the
/// availability slots load.
class MeetingSheetSpinner extends StatelessWidget {
  const MeetingSheetSpinner({super.key});

  @override
  Widget build(BuildContext context) => const Align(
        alignment: AlignmentDirectional.centerStart,
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: SimfTokens.space2),
          child: SizedBox(
            width: SimfTokens.space5,
            height: SimfTokens.space5,
            child: CircularProgressIndicator(
              strokeWidth: SimfTokens.meetingRequestSheetStrokeWidth,
              color: SimfTokens.accent,
            ),
          ),
        ),
      );
}

/// The picker's type-to-filter field (owner 2026-07-11) — the sheet's beige
/// field look with a leading magnifier so a VIP can filter a long roster
/// instead of scrolling. Reuses the speakers-list search hint.
class MeetingSearchField extends StatelessWidget {
  const MeetingSearchField({
    required this.fieldKey,
    required this.hintText,
    required this.onChanged,
    super.key,
  });

  final Key fieldKey;
  final String hintText;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) => TextField(
        key: fieldKey,
        onChanged: onChanged,
        style: SimfTokens.bodyInputMd,
        decoration: meetingSheetInputDecoration(
          hintText: hintText,
          isDense: true,
          horizontalPadding: SimfTokens.space3,
          prefixIcon: const Icon(
            Icons.search,
            color: SimfTokens.greyText,
            size: SimfTokens.meetingRequestSheetSize,
          ),
        ),
      );
}

/// The subject input — a white, beige-bordered field with the "اكتب الموضوع"
/// hint (Figma 1776:5048).
class MeetingSubjectField extends StatelessWidget {
  const MeetingSubjectField({
    required this.fieldKey,
    required this.controller,
    required this.hintText,
    super.key,
  });

  final Key fieldKey;
  final TextEditingController controller;
  final String hintText;

  @override
  Widget build(BuildContext context) => TextField(
        key: fieldKey,
        controller: controller,
        maxLength: FieldLimits.meetingRequestMessage,
        style: SimfTokens.bodyInputMd,
        decoration: meetingSheetInputDecoration(
          hintText: hintText,
          counterText: '',
          border: const OutlineInputBorder(),
        ),
      );
}

/// The one decoration behind every field on the sheet.
///
/// [isDense], [counterText], [prefixIcon] and [border] stay nullable rather
/// than taking a `false`/`''` default: passing a value where the field used to
/// be absent is NOT the same render, since each falls back to the input theme
/// when null.
///
/// OutlineInputBorder's default radius is circular-4
/// (== SimfTokens.borderRadiusSmall), so it is left implicit — passing it trips
/// avoid_redundant_argument_values (as in lookup_search_sheet).
InputDecoration meetingSheetInputDecoration({
  required String hintText,
  double horizontalPadding = SimfTokens.space4,
  String? counterText,
  bool? isDense,
  Widget? prefixIcon,
  InputBorder? border,
}) =>
    InputDecoration(
      counterText: counterText,
      isDense: isDense,
      hintText: hintText,
      hintStyle: SimfTokens.bodyGreyMd,
      prefixIcon: prefixIcon,
      filled: true,
      fillColor: SimfTokens.surface,
      contentPadding: EdgeInsets.symmetric(
        horizontal: horizontalPadding,
        vertical: SimfTokens.space3,
      ),
      enabledBorder: const OutlineInputBorder(
        borderSide: BorderSide(color: SimfTokens.beigeBorder),
      ),
      focusedBorder: const OutlineInputBorder(
        borderSide: BorderSide(color: SimfTokens.accent),
      ),
      border: border,
    );
