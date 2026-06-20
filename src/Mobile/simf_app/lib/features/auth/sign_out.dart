import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';

/// Confirms, then revokes the session (D-373) and lands on sign-in. Shared by
/// the المزيد page (Figma 1129:17224 "تسجيل الخروج") and the shell drawer so the
/// sign-out flow has one owner. The captured router survives the await; the
/// [context] is not used after it.
Future<void> confirmAndSignOut(
  BuildContext context,
  WidgetRef ref,
  AppL10n l10n,
) async {
  final router = GoRouter.of(context);
  final auth = ref.read(authControllerProvider.notifier);
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: Text(l10n.signOutLink),
      content: Text(l10n.signOutConfirmBody),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: Text(l10n.cancelLabel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: Text(l10n.signOutLink),
        ),
      ],
    ),
  );
  if (confirmed != true) {
    return;
  }
  await auth.signOut();
  router.goNamed(RouteNames.signIn);
}
