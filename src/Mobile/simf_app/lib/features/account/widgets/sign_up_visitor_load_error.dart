import 'dart:async';

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_refresh.dart';

/// The load-failure branch of the sign-up profile step: a centred message and
/// a retry, wrapped so the branch also pulls to refresh.
///
/// Kept local rather than swapped for the shared `SimfErrorState`: that one
/// draws white text, which is invisible on this screen's beige form card.
///
/// Only this branch is pull-to-refreshable. The loaded form must NOT be —
/// reloading re-applies the stored profile over every text controller, so a
/// stray pull on a half-filled form would silently discard the input. The rule
/// exists so nobody is stranded with no way to re-fetch, and that can only
/// happen here.
class SignUpVisitorLoadError extends StatelessWidget {
  const SignUpVisitorLoadError({
    required this.l10n,
    required this.onRefresh,
    super.key,
  });

  final AppL10n l10n;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    return SimfRefreshableMessage(
      onRefresh: onRefresh,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.profileLoadError,
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
