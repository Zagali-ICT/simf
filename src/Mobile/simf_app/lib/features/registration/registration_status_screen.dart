import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/registration/data/registration_providers.dart';
import 'package:simf_app/features/registration/widgets/registration_primary_button.dart';
import 'package:simf_app/features/registration/widgets/registration_secondary_button.dart';
import 'package:simf_app/features/registration/widgets/registration_sign_out_link.dart';
import 'package:simf_app/features/registration/widgets/registration_status_header.dart';
import 'package:simf_app/features/registration/widgets/registration_status_hero.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Registration status — route: RouteNames.registrationStatus · Figma
/// 1701:3789
/// Contract: a gate screen for a signed-in but not-yet-approved account. A
/// session-expired failure flips auth to signed-out and the router's auth gate
/// (route 11) redirects to sign-in.
class RegistrationStatusScreen extends ConsumerStatefulWidget {
  const RegistrationStatusScreen({super.key});

  @override
  ConsumerState<RegistrationStatusScreen> createState() =>
      _RegistrationStatusScreenState();
}

class _RegistrationStatusScreenState
    extends ConsumerState<RegistrationStatusScreen> {
  Future<void> _refresh() =>
      refreshAsync(ref, registrationStatusProvider.future);

  Future<void> _signOut() async {
    await ref.read(authControllerProvider.notifier).signOut();
    if (!mounted) {
      return;
    }
    context.goNamed(RouteNames.signIn);
  }

  void _continue() => context.go('/');

  void _back() {
    if (context.canPop()) {
      context.pop();
    } else {
      context.goNamed(RouteNames.signIn);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            RegistrationStatusHeader(
              title: l10n.registrationStatusTitle,
              onBack: _back,
            ),
            Expanded(child: _buildBody(l10n)),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(registrationStatusProvider).when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
          error: (_, __) => _buildError(l10n),
          data: (status) => _buildStatusView(l10n, status),
        );
  }

  Widget _buildStatusView(AppL10n l10n, RegistrationStatus status) {
    final Color color;
    final IconData icon;
    final String headline;
    final String message;
    String? primaryLabel;
    VoidCallback? onPrimary;
    switch (status) {
      case RegistrationStatus.pending:
        color = SimfTokens.accent;
        icon = Icons.schedule_rounded;
        headline = l10n.regPendingHeadline;
        message = l10n.regPendingMessage;
        primaryLabel = l10n.reCheckButton;
        onPrimary = () => ref.invalidate(registrationStatusProvider);
      case RegistrationStatus.approved:
        color =
            SimfTokens.statusAccepted; // #22C55E — the frame's approval green
        icon = Icons.check_rounded;
        headline = l10n.regApprovedHeadline;
        message = l10n.regApprovedMessage;
        primaryLabel = l10n.continueLabel;
        onPrimary = _continue;
      case RegistrationStatus.rejected:
        color = SimfTokens.danger;
        icon = Icons.close_rounded;
        headline = l10n.regRejectedHeadline;
        message = l10n.regRejectedMessage;
        primaryLabel = null;
        onPrimary = null;
    }

    // Vertically centred but scroll-safe on short screens; capped for tablets.
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space6,
        ),
        child: ConstrainedBox(
          constraints: BoxConstraints(
            minHeight: constraints.maxHeight - 2 * SimfTokens.space6,
          ),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(
                  maxWidth: SimfTokens.registrationStatusScreenMaxWidth,),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  RegistrationStatusHero(
                    color: color,
                    icon: icon,
                    headline: headline,
                    message: message,
                  ),
                  const SizedBox(height: SimfTokens.space6),
                  if (primaryLabel != null && onPrimary != null) ...<Widget>[
                    RegistrationPrimaryButton(
                      label: primaryLabel,
                      onTap: onPrimary,
                    ),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                  // A non-approved account gets an explicit way back to the
                  // (guest) home so it is never stuck on the gate (owner
                  // 2026-07-06); the approved state reaches home via its
                  // "متابعة" (Continue) primary instead.
                  if (status != RegistrationStatus.approved) ...<Widget>[
                    RegistrationSecondaryButton(
                      label: l10n.goHomeButton,
                      onTap: _continue,
                    ),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                  RegistrationSignOutLink(
                    label: l10n.signOutLink,
                    onTap: () => unawaited(_signOut()),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildError(AppL10n l10n) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              l10n.regStatusError,
              textAlign: TextAlign.center,
              style: SimfTokens.hintBeige,
            ),
            const SizedBox(height: SimfTokens.space4),
            RegistrationPrimaryButton(
              label: l10n.retryLabel,
              onTap: () => unawaited(_refresh()),
            ),
            const SizedBox(height: SimfTokens.space3),
            RegistrationSignOutLink(
              label: l10n.signOutLink,
              onTap: () => unawaited(_signOut()),
            ),
          ],
        ),
      ),
    );
  }
}
