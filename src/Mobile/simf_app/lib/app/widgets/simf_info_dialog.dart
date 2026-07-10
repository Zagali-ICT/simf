import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// App-wide single-action info dialog — the one-button sibling of
/// `SimfConfirmDialog` (which is deliberately hard-wired to two 50/50 actions,
/// D-565). Same navy chrome; one full-width OK button that just closes it.
/// Used where there is nothing to decide, only something to tell the user
/// (e.g. the About-screen update-check outcomes, D-736).
class SimfInfoDialog extends StatelessWidget {
  const SimfInfoDialog({required this.title, this.message, super.key});

  final String title;

  /// Optional body line under the title.
  final String? message;

  static Future<void> show(
    BuildContext context, {
    required String title,
    String? message,
  }) {
    return showDialog<void>(
      context: context,
      builder: (_) => SimfInfoDialog(title: title, message: message),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Dialog(
      backgroundColor: SimfTokens.navyDeep,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space5),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              title,
              style: theme.textTheme.titleMedium?.copyWith(color: Colors.white),
            ),
            if (message != null && message!.trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Text(message!, style: SimfTokens.bodyBeige),
            ],
            const SizedBox(height: SimfTokens.space5),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: () => Navigator.of(context).pop(),
                child: Text(AppL10n.of(context).okLabel),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
