import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/field_limits.dart';

/// The "الاسئلة" section label (frame 945:3756) over the fixed 100px tinted
/// question box (frame 934:3668): navyDeep fill on the 8px radius (no border),
/// the placeholder pinned top + beige + inline-end aligned, max 500 chars.
class SendQuestionComposer extends StatelessWidget {
  const SendQuestionComposer({
    required this.sectionLabel,
    required this.hint,
    required this.controller,
    required this.errorText,
    required this.onChanged,
    super.key,
  });

  final String sectionLabel;
  final String hint;
  final TextEditingController controller;
  final String? errorText;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Frame 945:3756 — white, Medium, aligned to the inline end (right in
        // RTL).
        Text(
          sectionLabel,
          // TextAlign.start = right under RTL (TextAlign.end would be left).
          textAlign: TextAlign.start,
          style: SimfTokens.labelWhiteMediumLg,
        ),
        const SizedBox(height: SimfTokens.space2),
        Container(
          height: SimfTokens.questionBoxHeight,
          decoration: BoxDecoration(
            color: SimfTokens.navyDeep,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space3,
          ),
          child: TextField(
            controller: controller,
            maxLength: FieldLimits.sessionQuestion,
            maxLines: null,
            expands: true,
            textAlignVertical: TextAlignVertical.top,
            textInputAction: TextInputAction.newline,
            style: SimfTokens.bodyWhiteSm,
            cursorColor: SimfTokens.accent,
            decoration: InputDecoration(
              isCollapsed: true,
              border: InputBorder.none,
              counterText: '',
              hintText: hint,
              hintStyle: SimfTokens.labelBeigeSm,
              errorText: errorText,
              errorStyle: SimfTokens.bodyDanger,
            ),
            onChanged: onChanged,
          ),
        ),
      ],
    );
  }
}
