import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The saved-contacts empty state: the contacts glyph, a bold title, a muted
/// hint and the scan-to-add CTA. Shown when the list resolves to no rows.
class ContactsEmptyState extends StatelessWidget {
  const ContactsEmptyState({
    required this.title,
    required this.hint,
    required this.actionLabel,
    required this.onAction,
    super.key,
  });

  final String title;
  final String hint;
  final String actionLabel;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.contacts_outlined,
              size: 56,
              color: SimfTokens.inkMuted,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              title,
              textAlign: TextAlign.center,
              style: SimfTokens.emphasisBold,
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(
              hint,
              textAlign: TextAlign.center,
              style: SimfTokens.bodyInkMuted,
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton.icon(
              onPressed: onAction,
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(actionLabel),
            ),
          ],
        ),
      ),
    );
  }
}
