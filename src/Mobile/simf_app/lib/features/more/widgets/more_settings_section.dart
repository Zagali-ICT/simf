import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/more/widgets/more_list.dart';

/// The الإعدادات group of the More menu (frame 1129:17224).
class MoreSettingsSection extends ConsumerWidget {
  const MoreSettingsSection({required this.accountEmail, super.key});

  /// The signed-in account's email, or null for a guest — the signed-in-only
  /// rows are hidden then.
  final String? accountEmail;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final email = accountEmail;
    return MoreSection(
      title: l10n.moreSectionSettings,
      rows: <Widget>[
        MoreRow(
          title: l10n.moreLanguage,
          trailingValue: l10n.languageCurrentName,
          onTap: () => unawaited(
            ref.read(localeControllerProvider.notifier).toggle(),
          ),
        ),
        MoreRow(
          title: l10n.moreAccessibility,
          onTap: () => context.pushNamed(RouteNames.accessibility),
        ),
        // Notifications are auth-only — hide from a not-logged-in guest
        // so the row doesn't dead-bounce to sign-in (D-669).
        if (email != null)
          MoreRow(
            title: l10n.moreNotifications,
            onTap: () => context.pushNamed(RouteNames.notifications),
          ),
        // Reset password (signed-in only) — reuses the forgot→reset
        // flow: it emails a code, then reset sets the new password.
        // The known email is pre-filled so it isn't retyped (D-659).
        if (email != null)
          MoreRow(
            title: l10n.moreResetPassword,
            onTap: () => context.pushNamed(
              RouteNames.forgotPassword,
              queryParameters: <String, String>{'email': email},
            ),
          ),
      ],
    );
  }
}
