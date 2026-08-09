import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/contact_us/widgets/contact_card_chrome.dart';
import 'package:simf_app/features/contact_us/widgets/contact_field.dart';

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
            ContactField(
              label: l10n.contactNameLabel,
              hint: l10n.contactNameHint,
              controller: name,
              textInputAction: TextInputAction.next,
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? l10n.contactNameRequired
                  : null,
            ),
            const SizedBox(height: SimfTokens.space4),
            ContactField(
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
            ContactField(
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
                        height: SimfTokens.space5,
                        width: SimfTokens.space5,
                        child: CircularProgressIndicator(strokeWidth: SimfTokens.contactSendMessageCardStrokeWidth),
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
