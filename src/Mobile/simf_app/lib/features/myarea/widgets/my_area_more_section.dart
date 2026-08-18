import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/myarea/widgets/my_area_more_row.dart';

/// The المزيد section of the My-Area dashboard (frame 213:963), which ends at
/// اعدادات الحساب — the calendar export and sign-out moved to the shell's side
/// drawer (D-396).
class MyAreaMoreSection extends StatelessWidget {
  const MyAreaMoreSection({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfSectionHeader(title: l10n.moreSectionSettings),
        const SizedBox(height: SimfTokens.space3),
        MyAreaMoreRow(
          label: l10n.smartBadgeLink,
          onTap: () => context.pushNamed(RouteNames.badge),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-500 (Wave 5, الطلبات) — the unified requests feed.
        MyAreaMoreRow(
          label: l10n.requestsLink,
          onTap: () => context.pushNamed(RouteNames.requests),
        ),
        const SizedBox(height: SimfTokens.space4),
        // #14 — edit the account's interests after sign-up (same interests page
        // as sign-up, in edit mode).
        MyAreaMoreRow(
          label: l10n.interestsTitle,
          onTap: () => context.pushNamed(RouteNames.myInterests),
        ),
        const SizedBox(height: SimfTokens.space4),
        // Owner 2026-07-26 — add / edit the mobile number (validate only, no
        // OTP), next to the other self-service profile edits.
        MyAreaMoreRow(
          label: l10n.myMobileTitle,
          onTap: () => context.pushNamed(RouteNames.myMobile),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-485 — the standalone Join-a-session hub (the seat-booking flow; the
        // other entry is the Join CTA on each session page).
        MyAreaMoreRow(
          label: l10n.joinHubTitle,
          onTap: () => context.pushNamed(RouteNames.joinSessionHub),
        ),
        const SizedBox(height: SimfTokens.space4),
        MyAreaMoreRow(
          label: l10n.moreTitle,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
        // Face-ID sign-in enable/disable (D-445) — self-hides when the device
        // has no usable biometric. Also offered in the side menu.
        const FaceIdToggleTile(),
      ],
    );
  }
}
