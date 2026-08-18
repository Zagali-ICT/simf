import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/features/account/sign_out.dart';

/// The tail of the More menu: the تسجيل الخروج link (signed-in only) over the
/// static version line.
class MoreFooter extends ConsumerWidget {
  const MoreFooter({required this.signedIn, super.key});

  final bool signedIn;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (signedIn) ...<Widget>[
          const SizedBox(height: SimfTokens.space6),
          Center(
            child: TextButton(
              onPressed: () =>
                  unawaited(confirmAndSignOut(context, ref, l10n)),
              child: Text(
                l10n.signOutLink,
                style: SimfTokens.bodyBeigeMd,
              ),
            ),
          ),
        ],
        const SizedBox(height: SimfTokens.space4),
        Center(
          child: Text(
            // D-736 — the real installed version (package_info_plus).
            l10n.moreVersionLine(ref.watch(installedAppVersionProvider)),
            style: SimfTokens.bodyInkMutedSm,
          ),
        ),
      ],
    );
  }
}
