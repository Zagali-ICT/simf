import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/external_link.dart';
import '../localization/app_l10n.dart';
import 'simf_confirm_dialog.dart';

/// Confirms with the user before leaving the app for an external website, then
/// launches it in the external browser (owner 2026-06-27 — every external link
/// asks first, so a tap never silently navigates away). A blank [url] is a
/// no-op (the control is inert when unconfigured, D-369). Bilingual via
/// [AppL10n]; uses the shared [SimfConfirmDialog] (two buttons in one row).
Future<void> confirmThenLaunchExternal(BuildContext context, String url) async {
  if (url.trim().isEmpty) {
    return;
  }
  final l10n = AppL10n.of(context);
  final confirmed = await SimfConfirmDialog.show(
    context,
    title: l10n.externalLinkTitle,
    message: l10n.externalLinkBody,
    confirmLabel: l10n.externalLinkOpen,
  );
  if (confirmed) {
    await launchExternalUri(
      Uri.parse(url),
      mode: LaunchMode.externalApplication,
    );
  }
}
