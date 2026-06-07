import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 011 — حالة التسجيل · Registration status (Page_011 docs).
///
/// A gate screen for a signed-in but **not-yet-approved** account. On open (and
/// on every Re-check) it calls `GET /app/users/me` via
/// [AuthController.refreshCurrentUser] and renders the state for the returned
/// `registrationStatus`: **Pending** (under-review + Re-check), **Approved**
/// (Continue → app), **Rejected** (declined copy). A wire failure shows the
/// **Error** state with retry; a session-expired failure flips auth to signed-out
/// and the router's auth gate (route 11) redirects to sign-in. The approval
/// reference/date are decoration only (D11) and are not rendered here.
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.registrationStatusTitle),
        automaticallyImplyLeading: false,
        actions: <Widget>[
          TextButton(
            onPressed: () => unawaited(_signOut()),
            child: Text(l10n.signOutLink),
          ),
          const SizedBox(width: SimfTokens.space2),
        ],
      ),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error || _status == null) {
      return _buildError(l10n);
    }
    return _buildStatusView(l10n, _status!);
  }

  Widget _buildStatusView(AppL10n l10n, RegistrationStatus status) {
    final IconData icon;
    final Color color;
    final String headline;
    final String message;
    Widget? primary;
    switch (status) {
      case RegistrationStatus.pending:
        icon = Icons.schedule_outlined;
        color = SimfTokens.accent;
        headline = l10n.regPendingHeadline;
        message = l10n.regPendingMessage;
        primary = FilledButton(
          onPressed: () => unawaited(_load()),
          child: Text(l10n.reCheckButton),
        );
      case RegistrationStatus.approved:
        icon = Icons.check_rounded;
        color = SimfTokens.success;
        headline = l10n.regApprovedHeadline;
        message = l10n.regApprovedMessage;
        primary = FilledButton(
          onPressed: _continue,
          child: Text(l10n.continueLabel),
        );
      case RegistrationStatus.rejected:
        icon = Icons.close_rounded;
        color = SimfTokens.danger;
        headline = l10n.regRejectedHeadline;
        message = l10n.regRejectedMessage;
        primary = null;
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _StatusCard(
            icon: icon,
            color: color,
            headline: headline,
            message: message,
          ),
          if (status != RegistrationStatus.rejected) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            _StagesTracker(status: status, l10n: l10n),
          ],
          if (primary != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            primary,
          ],
        ],
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
            Text(l10n.regStatusError, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: () => unawaited(_load()),
              child: Text(l10n.retryLabel),
            ),
          ],
        ),
      ),
    );
  }
}

/// The status hero (Page_011 mockup): a centred card with a circular badge
/// holding the state icon, an accent/state headline, and a secondary message.
/// The fill, border and badge take the state [color] (accent while Pending).
class _StatusCard extends StatelessWidget {
  const _StatusCard({
    required this.icon,
    required this.color,
    required this.headline,
    required this.message,
  });

  final IconData icon;
  final Color color;
  final String headline;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.06),
        border: Border.all(color: color),
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: 48,
            height: 48,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              border: Border.all(color: color, width: 1.5),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, size: 22, color: color),
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            headline,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
          const SizedBox(height: SimfTokens.space1),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              fontSize: SimfTokens.textSm,
              height: 1.7,
            ),
          ),
        ],
      ),
    );
  }
}

/// The static four-step registration progress tracker (Page_011). Steps 1–2 are
/// always complete; step 3 (team review) is the current step while Pending and
/// complete on Approved; step 4 (activation) completes on Approved.
class _StagesTracker extends StatelessWidget {
  const _StagesTracker({required this.status, required this.l10n});

  final RegistrationStatus status;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final approved = status == RegistrationStatus.approved;
    final steps = <_StageRow>[
      _StageRow(l10n.stageDataSubmitted, _StageState.complete, 1),
      _StageRow(l10n.stageEmailConfirmed, _StageState.complete, 2),
      _StageRow(
        l10n.stageTeamReview,
        approved ? _StageState.complete : _StageState.current,
        3,
      ),
      _StageRow(
        l10n.stageActivation,
        approved ? _StageState.complete : _StageState.future,
        4,
      ),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        border: Border.all(color: SimfTokens.line),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.stagesTitle,
            style: const TextStyle(
              fontSize: SimfTokens.textXs,
              fontWeight: FontWeight.w600,
              color: SimfTokens.txtTertiary,
              letterSpacing: 1.2,
            ),
          ),
          for (final step in steps)
            Padding(
              padding: const EdgeInsets.only(top: SimfTokens.space3),
              child: Row(
                children: <Widget>[
                  _StageBadge(step),
                  const SizedBox(width: SimfTokens.space3),
                  Expanded(
                    child: Text(
                      step.label,
                      style: TextStyle(
                        fontSize: SimfTokens.textSm,
                        color: step.state == _StageState.future
                            ? SimfTokens.txtTertiary
                            : SimfTokens.surface,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// The 18px circular step marker: filled accent + navy tick when complete, an
/// accent-bordered accent dot when current, a hairline-bordered muted number
/// when still in the future (matches the Page_011 mockup badges).
class _StageBadge extends StatelessWidget {
  const _StageBadge(this.step);

  final _StageRow step;

  @override
  Widget build(BuildContext context) {
    final Color background;
    final Color foreground;
    final BoxBorder? border;
    switch (step.state) {
      case _StageState.complete:
        background = SimfTokens.accent;
        foreground = SimfTokens.navy;
        border = null;
      case _StageState.current:
        background = Colors.transparent;
        foreground = SimfTokens.accent;
        border = Border.all(color: SimfTokens.accent, width: 1.5);
      case _StageState.future:
        background = Colors.transparent;
        foreground = SimfTokens.txtTertiary;
        border = Border.all(color: SimfTokens.line);
    }
    return Container(
      width: 18,
      height: 18,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: background,
        border: border,
        shape: BoxShape.circle,
      ),
      child: step.state == _StageState.complete
          ? const Icon(Icons.check_rounded, size: 11, color: SimfTokens.navy)
          : step.state == _StageState.current
              ? Icon(Icons.circle, size: 8, color: foreground)
              : Text(
                  '${step.number}',
                  style: TextStyle(
                    fontSize: SimfTokens.textXs,
                    fontWeight: FontWeight.w600,
                    color: foreground,
                  ),
                ),
    );
  }
}

enum _StageState { complete, current, future }

class _StageRow {
  const _StageRow(this.label, this.state, this.number);

  final String label;
  final _StageState state;
  final int number;
}
