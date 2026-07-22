import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One labelled text field (navy fill, beige hairline, white text) — the shared
/// input for the "أرسل رسالة" contact form (name / email / message).
class ContactField extends StatelessWidget {
  const ContactField({
    required this.label,
    required this.hint,
    required this.controller,
    this.validator,
    this.keyboardType,
    this.textInputAction,
    this.maxLines = 1,
    super.key,
  });

  final String label;
  final String hint;
  final TextEditingController controller;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: SimfTokens.labelBeigeMediumSm, // Figma 1388:7778 — beige label
        ),
        const SizedBox(height: SimfTokens.space2),
        TextFormField(
          controller: controller,
          validator: validator,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          maxLines: maxLines,
          style: SimfTokens.bodyWhiteMd,
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: SimfTokens.hintBeige,
            filled: true,
            // Same fill as the card (border-only field) — Figma 1388:7779.
            fillColor: SimfTokens.navyDeep,
            contentPadding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space3,
              vertical: SimfTokens.space3,
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.beigeBorder),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.accent),
            ),
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.beigeBorder),
            ),
          ),
        ),
      ],
    );
  }
}
