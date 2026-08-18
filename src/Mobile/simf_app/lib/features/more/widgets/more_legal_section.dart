import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/features/more/widgets/more_list.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The قانوني group of the More menu (frame 1129:17224).
class MoreLegalSection extends StatelessWidget {
  const MoreLegalSection({required this.role, super.key});

  /// The signed-in user's effective app role, [AppRole.guest] when signed out.
  final AppRole role;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return MoreSection(
      title: l10n.moreSectionLegal,
      rows: <Widget>[
        MoreRow(
          title: l10n.moreTerms,
          onTap: () => context.pushNamed(RouteNames.terms),
        ),
        MoreRow(
          title: l10n.contactUsTitle,
          onTap: () => context.pushNamed(RouteNames.contactUs),
        ),
        if (routeAllowsRole(RouteNames.rate, role))
          MoreRow(
            title: l10n.moreRateApp,
            onTap: () => context.pushNamed(RouteNames.rate),
          ),
      ],
    );
  }
}
