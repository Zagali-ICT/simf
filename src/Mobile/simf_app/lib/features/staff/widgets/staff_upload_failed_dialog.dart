import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// DEF-STF-004 — names exactly which attachment did not land and offers a retry
/// of the UPLOAD for the already-created visitor, so the person is never
/// registered twice. Pops true to retry, false to skip.
class StaffUploadFailedDialog extends StatelessWidget {
  const StaffUploadFailedDialog({required this.pendingLabels, super.key});

  final List<String> pendingLabels;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return AlertDialog(
      title: Text(l10n.staffUploadFailedTitle),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(l10n.staffUploadFailedIntro),
          const SizedBox(height: SimfTokens.space2),
          for (final label in pendingLabels) Text('• $label'),
        ],
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(l10n.staffUploadSkipLabel),
        ),
        FilledButton(
          key: const ValueKey<String>('staffUploadRetry'),
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(l10n.staffUploadRetryLabel),
        ),
      ],
    );
  }
}
