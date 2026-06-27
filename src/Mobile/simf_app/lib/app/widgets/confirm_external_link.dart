import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/external_link.dart';
import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// Confirms with the user before leaving the app for an external website, then
/// launches it in the external browser (owner 2026-06-27 — every external link
/// asks first, so a tap never silently navigates away). A blank [url] is a
/// no-op (the control is inert when unconfigured, D-369). Bilingual via
/// [AppL10n]; styled to the navy theme.
Future<void> confirmThenLaunchExternal(BuildContext context, String url) async {
  if (url.trim().isEmpty) {
    return;
  }
  final l10n = AppL10n.of(context);
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      backgroundColor: SimfTokens.navyDeep,
      title: Text(
        l10n.externalLinkTitle,
        style: const TextStyle(color: Colors.white),
      ),
      content: Text(
        l10n.externalLinkBody,
        style: const TextStyle(color: SimfTokens.beigeBorder),
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(ctx).pop(false),
          child: Text(l10n.cancelLabel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(ctx).pop(true),
          child: Text(l10n.externalLinkOpen),
        ),
      ],
    ),
  );
  if (confirmed ?? false) {
    await launchExternalUri(
      Uri.parse(url),
      mode: LaunchMode.externalApplication,
    );
  }
}
