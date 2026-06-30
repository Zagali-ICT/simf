import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// App-wide confirm dialog whose two actions are ALWAYS laid out side-by-side
/// in one row (owner 2026-06-30, D-565). Flutter's default [AlertDialog] feeds
/// its `actions` through an [OverflowBar] that stacks them into a column once
/// the (Arabic) labels are too wide; this keeps Cancel + confirm on the same
/// row at 50/50 width regardless of label length. Navy-themed to match the app.
///
/// Use [SimfConfirmDialog.show] — it resolves to `true` only when the user
/// confirms (a dismiss / Cancel resolves to `false`).
class SimfConfirmDialog extends StatelessWidget {
  const SimfConfirmDialog({
    required this.title,
    required this.confirmLabel,
    required this.cancelLabel,
    this.message,
    this.isDestructive = false,
    super.key,
  });

  final String title;

  /// Optional body line under the title. When null/empty the title alone is the
  /// question (e.g. "Cancel this request?").
  final String? message;
  final String confirmLabel;
  final String cancelLabel;

  /// Tints the confirm button with the error colour for a destructive action
  /// (sign out, cancel a booking, remove a contact).
  final bool isDestructive;

  /// Shows the dialog and resolves to `true` only when confirmed. [cancelLabel]
  /// defaults to the shared [AppL10n.cancelLabel].
  static Future<bool> show(
    BuildContext context, {
    required String title,
    required String confirmLabel,
    String? message,
    String? cancelLabel,
    bool isDestructive = false,
  }) async {
    final l10n = AppL10n.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => SimfConfirmDialog(
        title: title,
        message: message,
        confirmLabel: confirmLabel,
        cancelLabel: cancelLabel ?? l10n.cancelLabel,
        isDestructive: isDestructive,
      ),
    );
    return confirmed ?? false;
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
            // Expanded inside a Row guarantees the two buttons share the width
            // 50/50 and never stack — the whole point of this widget.
            Row(
              children: <Widget>[
                Expanded(
                  child: TextButton(
                    onPressed: () => Navigator.of(context).pop(false),
                    child: Text(cancelLabel),
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: FilledButton(
                    style: isDestructive
                        ? FilledButton.styleFrom(
                            backgroundColor: theme.colorScheme.error,
                          )
                        : null,
                    onPressed: () => Navigator.of(context).pop(true),
                    child: Text(confirmLabel),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
