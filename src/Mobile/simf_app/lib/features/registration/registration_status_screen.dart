import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

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
/// the state icon, a white headline, a beige message), the "المراحل" progress
/// card, the gold primary button, and a "تسجيل الخروج" link beneath it.
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
            _Header(title: l10n.registrationStatusTitle, onBack: _back),
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
                  _Hero(
                    color: color,
                    icon: icon,
                    headline: headline,
                    message: message,
                  ),
                  if (status != RegistrationStatus.rejected) ...<Widget>[
                    const SizedBox(height: SimfTokens.space6),
                    _StagesCard(status: status, l10n: l10n),
                  ],
                  const SizedBox(height: SimfTokens.space6),
                  if (primaryLabel != null && onPrimary != null)
                    _PrimaryButton(label: primaryLabel, onTap: onPrimary),
                  const SizedBox(height: SimfTokens.space3),
                  _SignOutLink(label: l10n.signOutLink, onTap: () => unawaited(_signOut())),
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
            _PrimaryButton(label: l10n.retryLabel, onTap: () => unawaited(_load())),
            const SizedBox(height: SimfTokens.space3),
            _SignOutLink(label: l10n.signOutLink, onTap: () => unawaited(_signOut())),
          ],
        ),
      ),
    );
  }
}

/// Back chevron (inline start) + centred title (Figma 1701:3794/3796). A plain
/// white chevron — the gate frame does not use the standard circled back box.
class _Header extends StatelessWidget {
  const _Header({required this.title, required this.onBack});

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    // LTR so the chevron sits at the physical left and the title stays centred,
    // matching the frame under Arabic/RTL.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        child: Row(
          children: <Widget>[
            IconButton(
              onPressed: onBack,
              icon: const Icon(
                Icons.arrow_back_ios_new,
                color: Colors.white,
                size: 20,
              ),
              splashRadius: 22,
            ),
            Expanded(
              child: Text(
                title,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: SimfTokens.textTitle,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
                ),
              ),
            ),
            const SizedBox(width: 48), // balances the back button; keeps title centred
          ],
        ),
      ),
    );
  }
}

/// The status hero (Figma 1701:3799–3804): a 104px ring (navyDeep fill, a
/// state-coloured 2.36px border) around the state icon, a white headline, and a
/// beige secondary message. The ring + icon carry the state [color]; the
/// headline is always white.
class _Hero extends StatelessWidget {
  const _Hero({
    required this.color,
    required this.icon,
    required this.headline,
    required this.message,
  });

  final Color color;
  final IconData icon;
  final String headline;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Container(
          width: 104,
          height: 104,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.navyDeep,
            shape: BoxShape.circle,
            border: Border.all(color: color, width: 2.36),
          ),
          child: Icon(icon, size: 40, color: color),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          headline,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: SimfTokens.text24, // 24
            fontWeight: FontWeight.w700,
            color: Colors.white,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          message,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textMd, // 14
            height: 1.5,
          ),
        ),
      ],
    );
  }
}

/// The four-step registration progress card (Figma 1701:3805–3822): a navy-80%
/// card with a right-aligned "المراحل" title and one right-aligned row per
/// stage — a white label with its completion marker at the inline end. Steps 1–2
/// are always complete; step 3 (team review) is the current step while Pending
/// and complete on Approved; step 4 (activation) completes on Approved.
class _StagesCard extends StatelessWidget {
  const _StagesCard({required this.status, required this.l10n});

  final RegistrationStatus status;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final approved = status == RegistrationStatus.approved;
    final steps = <_Stage>[
      _Stage(l10n.stageDataSubmitted, _StageState.complete, 1),
      _Stage(l10n.stageEmailConfirmed, _StageState.complete, 2),
      _Stage(
        l10n.stageTeamReview,
        approved ? _StageState.complete : _StageState.current,
        3,
      ),
      _Stage(
        l10n.stageActivation,
        approved ? _StageState.complete : _StageState.future,
        4,
      ),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navy.withValues(alpha: 0.8),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      // crossAxisAlignment.start = the inline start (right under RTL), so the
      // title and every row hug the right edge like the frame.
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.stagesTitle,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd, // 14
            ),
          ),
          for (final step in steps)
            Padding(
              padding: const EdgeInsets.only(top: SimfTokens.space2),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    step.label,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textMd, // 14
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  _StageMarker(step),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// The 18px stage marker: a gold done-check when complete, a gold ring while the
/// step is current, and a muted numbered circle while it is still ahead.
class _StageMarker extends StatelessWidget {
  const _StageMarker(this.step);

  final _Stage step;

  @override
  Widget build(BuildContext context) {
    switch (step.state) {
      case _StageState.complete:
        return const Icon(
          Icons.check_rounded,
          size: 18,
          color: SimfTokens.accent,
        );
      case _StageState.current:
        return Container(
          width: 18,
          height: 18,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: SimfTokens.accent, width: 1.5),
          ),
          child: const Icon(Icons.circle, size: 8, color: SimfTokens.accent),
        );
      case _StageState.future:
        return Container(
          width: 18,
          height: 18,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: SimfTokens.line),
          ),
          child: Text(
            '${step.number}',
            style: const TextStyle(
              color: SimfTokens.txtTertiary,
              fontSize: SimfTokens.textXs, // ~11
              fontWeight: FontWeight.w600,
            ),
          ),
        );
    }
  }
}

enum _StageState { complete, current, future }

class _Stage {
  const _Stage(this.label, this.state, this.number);

  final String label;
  final _StageState state;
  final int number;
}

/// The full-width gold primary button (Figma 1701:3826): h48, radius-4, white
/// bold label.
class _PrimaryButton extends StatelessWidget {
  const _PrimaryButton({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      width: double.infinity,
      child: Material(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          child: Center(
            child: Text(
              label,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The "تسجيل الخروج" link beneath the primary button (Figma 1701:3827) —
/// centred, muted, 12px.
class _SignOutLink extends StatelessWidget {
  const _SignOutLink({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return TextButton(
      onPressed: onTap,
      style: TextButton.styleFrom(
        minimumSize: const Size.fromHeight(36),
        foregroundColor: SimfTokens.beigeBorder,
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textSm, // 12–14
        ),
      ),
    );
  }
}
