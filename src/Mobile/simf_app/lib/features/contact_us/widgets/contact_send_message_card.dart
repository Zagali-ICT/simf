import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import 'contact_card_chrome.dart';

/// The "أرسل رسالة" form card (frame node 1388:7711): name / email / message
/// fields over the gold send button, on the navy-deep card chrome.
class ContactSendMessageCard extends StatelessWidget {
  const ContactSendMessageCard({
    required this.formKey,
    required this.name,
    required this.email,
    required this.message,
    required this.sending,
    required this.onSend,
    super.key,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController name;
  final TextEditingController email;
  final TextEditingController message;
  final bool sending;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return ContactCard(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            ContactCardHeading(l10n.contactSendTitle),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactNameLabel,
              hint: l10n.contactNameHint,
              controller: name,
              textInputAction: TextInputAction.next,
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? l10n.contactNameRequired
                  : null,
            ),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactEmailLabel,
              hint: l10n.contactEmailHint,
              controller: email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              validator: (v) {
                final value = (v ?? '').trim();
                if (value.isEmpty ||
                    !value.contains('@') ||
                    !value.contains('.')) {
                  return l10n.contactEmailInvalid;
                }
                return null;
              },
            ),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactMessageLabel,
              hint: l10n.contactMessageHint,
              controller: message,
              maxLines: 5,
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? l10n.contactMessageRequired
                  : null,
            ),
            const SizedBox(height: SimfTokens.space5),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: sending ? null : onSend,
                child: sending
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(l10n.contactSendButton),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One labelled text field (navy fill, beige hairline, white text).
class _Field extends StatelessWidget {
  const _Field({
    required this.label,
    required this.hint,
    required this.controller,
    this.validator,
    this.keyboardType,
    this.textInputAction,
    this.maxLines = 1,
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
          style: const TextStyle(
            color: SimfTokens.beigeBorder, // Figma 1388:7778 — beige label
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w500,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        TextFormField(
          controller: controller,
          validator: validator,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          maxLines: maxLines,
          style:
              const TextStyle(color: Colors.white, fontSize: SimfTokens.textMd),
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: const TextStyle(color: SimfTokens.beigeBorder),
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
