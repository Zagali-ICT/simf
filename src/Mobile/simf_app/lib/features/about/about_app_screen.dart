import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/widgets/about_app_body.dart';

/// About the app — عن التطبيق · route: RouteNames.aboutApp
/// Figma: no bound node. `1116-16448` is About the FORUM (AboutScreen) — a
///   different screen; do not bind it here. D-668 · D-736 (the real installed
///   version + the manual update check).
/// Contract: only the contact fields the admin actually set are rendered.
class AboutAppScreen extends ConsumerWidget {
  const AboutAppScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.aboutAppTitle,
      onBack: () => backOrHome(context),
      body: SimfPullToRefresh(
        onRefresh: () => ref.read(orgProfileProvider.notifier).warm(),
        child: const AboutAppBody(),
      ),
    );
  }
}
