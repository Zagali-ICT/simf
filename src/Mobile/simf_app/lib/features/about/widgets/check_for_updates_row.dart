import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/widgets/simf_confirm_dialog.dart';
import '../../../app/widgets/simf_info_dialog.dart';
import '../../../core/external_link.dart';
import '../../../core/startup/app_update_checker.dart';
import '../../../core/startup/app_version_policy.dart';
import '../../more/widgets/more_list.dart';

/// D-736 — the About-the-app "Check for updates" row: fetches the server
/// version policy on demand and tells the user explicitly what it found —
/// up to date (with the current version), an update available (Update → the
/// configured store listing), or an honest network error. Unlike the silent
/// launch check this one always answers, and it deliberately ignores the
/// soft-update snooze.
class CheckForUpdatesRow extends ConsumerStatefulWidget {
  const CheckForUpdatesRow({super.key});

  @override
  ConsumerState<CheckForUpdatesRow> createState() => _CheckForUpdatesRowState();
}

class _CheckForUpdatesRowState extends ConsumerState<CheckForUpdatesRow> {
  bool _checking = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return MoreRow(
      title: l10n.aboutCheckForUpdates,
      trailingValue: _checking ? l10n.updateCheckingLabel : null,
      onTap: _checking ? () {} : () => unawaited(_check()),
    );
  }

  Future<void> _check() async {
    setState(() => _checking = true);
    final l10n = AppL10n.of(context);
    try {
      final policy = await ref
          .read(appVersionPolicyRepositoryProvider)
          .fetch()
          .timeout(const Duration(seconds: 10));
      final platform = policy.currentPlatform;
      final installed = ref.read(installedAppVersionProvider);
      final status = evaluateVersionPolicy(
        installedVersion: installed,
        policy: platform,
      );
      if (!mounted) {
        return;
      }
      if (status == AppUpdateStatus.upToDate) {
        await SimfInfoDialog.show(
          context,
          title: l10n.aboutUpToDateTitle,
          message: l10n.aboutUpToDateBody(installed),
        );
        return;
      }
      // optional or forced — offer the update; the hard gate itself stays the
      // splash's job (Page_001 L-2). evaluateVersionPolicy guarantees a usable
      // store URL for any non-up-to-date status, so this bang is safe.
      // Show the PARSED version the decision was based on (min for a forced
      // status, latest otherwise) — not the raw string, so an unparseable admin
      // value drops to the generic body rather than rendering as a version.
      final target = status == AppUpdateStatus.forced
          ? tryParseVersion(platform.minVersion)
          : tryParseVersion(platform.latestVersion);
      final update = await SimfConfirmDialog.show(
        context,
        title: l10n.updateOptionalTitle,
        message: l10n.aboutUpdateAvailableBody(target?.toString() ?? ''),
        confirmLabel: l10n.updateNowLabel,
        cancelLabel: l10n.updateLaterLabel,
      );
      if (update) {
        await launchExternalUri(
          Uri.parse(usableStoreUrl(platform.storeUrl)!),
          mode: LaunchMode.externalApplication,
        );
      }
    } on Object {
      // An honest failure — never a fake "up to date".
      if (mounted) {
        await SimfInfoDialog.show(
          context,
          title: l10n.errorTitle,
          message: l10n.networkErrorBody,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _checking = false);
      }
    }
  }
}
