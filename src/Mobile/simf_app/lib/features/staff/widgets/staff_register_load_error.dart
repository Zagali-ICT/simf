import 'dart:async';

import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_refresh.dart';

/// The failed-lookup branch of the walk-in registration form, and the only part
/// of it that is pull-to-refreshable (13.6): [onRefresh] repopulates the
/// country / profile-type / organisation lookups, and re-running it over a
/// part-filled registration would reset the desk agent's entries.
class StaffRegisterLoadError extends StatelessWidget {
  const StaffRegisterLoadError({required this.onRefresh, super.key});

  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfRefreshableMessage(
      onRefresh: onRefresh,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.staffRegisterError,
                textAlign: TextAlign.center,
                style: const TextStyle(color: SimfTokens.txtSecondary),
              ),
              const SizedBox(height: SimfTokens.space4),
              FilledButton(
                onPressed: () => unawaited(onRefresh()),
                child: Text(l10n.retryLabel),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
