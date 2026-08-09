import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

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
  final confirmed = await SimfConfirmDialog.show(
    context,
    title: l10n.signOutLink,
    message: l10n.signOutConfirmBody,
    confirmLabel: l10n.signOutLink,
    isDestructive: true,
  );
  if (!confirmed) {
    return;
  }
  await auth.signOut();
  router.goNamed(RouteNames.signIn);
}
