import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'widgets/registration_primary_button.dart';
import 'widgets/registration_sign_out_link.dart';
import 'widgets/registration_status_header.dart';
import 'widgets/registration_status_hero.dart';

/// Page 011 — حالة التسجيل · Registration status (Figma **1701:3789**).
///
/// A gate screen for a signed-in but **not-yet-approved** account. On open (and
/// on every Re-check) it calls `GET /app/users/me` via
/// [AuthController.refreshCurrentUser] and renders the state for the returned
/// `registrationStatus`: **Pending** (under-review + Re-check), **Approved**
/// (Continue → app), **Rejected** (declined copy). A wire failure shows the
/// **Error** state with retry; a session-expired failure flips auth to signed-out
/// and the router's auth gate (route 11) redirects to sign-in.
///
/// Layout matches the frame: a `navySurface` gate (no bottom nav), a back +
/// centred title header, a vertically-centred hero (a state-coloured ring around
/// the state icon, a white headline, a beige message), the gold primary button,
/// and a "تسجيل الخروج" link beneath it.
class RegistrationStatusScreen extends ConsumerStatefulWidget {
  const RegistrationStatusScreen({super.key});

  @override
  ConsumerState<RegistrationStatusScreen> createState() =>
      _RegistrationStatusScreenState();
}

class _RegistrationStatusScreenState
    extends ConsumerState<RegistrationStatusScreen> {
  bool _loading = true;
  bool _error = false;
  RegistrationStatus? _status;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final user =
          await ref.read(authControllerProvider.notifier).refreshCurrentUser();
      if (!mounted) {
        return;
      }
      setState(() {
        _status = user.registrationStatus;
        _loading = false;
      });
    } on AuthFailure {
      if (!mounted) {
        return;
      }
      // A session-expired failure flips AuthState to SignedOut and the router's
      // auth gate redirects to sign-in; other failures show the Error state.
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

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
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_error || _status == null) {
      return _buildError(l10n);
    }
    return _buildStatusView(l10n, _status!);
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
        onPrimary = () => unawaited(_load());
      case RegistrationStatus.approved:
        color = SimfTokens.statusAccepted; // #22C55E — the frame's approval green
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
              constraints: const BoxConstraints(maxWidth: 480),
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
                  if (primaryLabel != null && onPrimary != null)
                    RegistrationPrimaryButton(
                      label: primaryLabel,
                      onTap: onPrimary,
                    ),
                  const SizedBox(height: SimfTokens.space3),
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
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
            const SizedBox(height: SimfTokens.space4),
            RegistrationPrimaryButton(
              label: l10n.retryLabel,
              onTap: () => unawaited(_load()),
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
