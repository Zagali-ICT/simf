import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/app/widgets/confirm_external_link.dart';
import 'package:simf_app/core/env/build_config.dart';
import 'package:simf_app/features/more/widgets/more_list.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The معلومات الملتقى group of the More menu (frame 1129:17224). Unbuilt
/// entries route to the ComingSoon placeholder; اكتشف السعودية opens
/// VisitSaudi.
class MoreForumInfoSection extends StatelessWidget {
  const MoreForumInfoSection({required this.role, super.key});

  /// The signed-in user's effective app role, [AppRole.guest] when signed out.
  final AppRole role;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return MoreSection(
      title: l10n.moreSectionForumInfo,
      rows: <Widget>[
        MoreRow(
          title: l10n.moreAbout,
          onTap: () => context.pushNamed(RouteNames.aboutForum),
        ),
        MoreRow(
          title: l10n.moreForumGuide,
          onTap: () => context.pushNamed(RouteNames.forumGuide),
        ),
        MoreRow(
          title: l10n.faqRowTitle,
          onTap: () => context.pushNamed(RouteNames.faq),
        ),
        // "عروض الجلسات" — My sessions (Figma 1388:9067). Restored
        // to the More menu 2026-07-09 (D-710, owner reversed the D-609
        // removal). The route is attendee-gated, so the row shows
        // only when a signed-in role may reach it.
        if (routeAllowsRole(RouteNames.myAreaSessions, role))
          MoreRow(
            title: l10n.mySessionsTitle,
            onTap: () => context.pushNamed(RouteNames.myAreaSessions),
          ),
        MoreRow(
          title: l10n.moreVisitSaudi,
          onTap: () => unawaited(
            confirmThenLaunchExternal(
              context,
              BuildConfig.visitSaudiUrl,
            ),
          ),
        ),
      ],
    );
  }
}
