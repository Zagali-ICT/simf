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
        icon = Icons.hourglass_top_outlined;
        color = SimfTokens.accent;
        headline = l10n.regPendingHeadline;
        message = l10n.regPendingMessage;
        primary = FilledButton(
          onPressed: () => unawaited(_load()),
          child: Text(l10n.reCheckButton),
        );
      case RegistrationStatus.approved:
        icon = Icons.check_circle_outline;
        color = SimfTokens.success;
        headline = l10n.regApprovedHeadline;
        message = l10n.regApprovedMessage;
        primary = FilledButton(
          onPressed: _continue,
          child: Text(l10n.continueLabel),
        );
      case RegistrationStatus.rejected:
        icon = Icons.cancel_outlined;
        color = SimfTokens.danger;
        headline = l10n.regRejectedHeadline;
        message = l10n.regRejectedMessage;
        primary = null;
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(SimfTokens.space6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space5),
          Icon(icon, size: 72, color: color),
          const SizedBox(height: SimfTokens.space4),
          Text(
            headline,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: SimfTokens.textXl,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.inkMuted),
          ),
          const SizedBox(height: SimfTokens.space6),
          if (status != RegistrationStatus.rejected) ...<Widget>[
            _StagesTracker(status: status, l10n: l10n),
            const SizedBox(height: SimfTokens.space6),
          ],
          if (primary != null) primary,
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
      _StageRow(l10n.stageDataSubmitted, _StageState.complete),
      _StageRow(l10n.stageEmailConfirmed, _StageState.complete),
      _StageRow(
        l10n.stageTeamReview,
        approved ? _StageState.complete : _StageState.current,
      ),
      _StageRow(
        l10n.stageActivation,
        approved ? _StageState.complete : _StageState.future,
      ),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          l10n.stagesTitle,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: SimfTokens.space2),
        for (final step in steps)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space1),
            child: Row(
              children: <Widget>[
                Icon(step.icon, size: 18, color: step.color),
                const SizedBox(width: SimfTokens.space2),
                Expanded(child: Text(step.label)),
              ],
            ),
          ),
      ],
    );
  }
}

enum _StageState { complete, current, future }

class _StageRow {
  const _StageRow(this.label, this.state);

  final String label;
  final _StageState state;

  IconData get icon => switch (state) {
        _StageState.complete => Icons.check_circle,
        _StageState.current => Icons.radio_button_checked,
        _StageState.future => Icons.radio_button_unchecked,
      };

  Color get color => switch (state) {
        _StageState.complete => SimfTokens.success,
        _StageState.current => SimfTokens.accent,
        _StageState.future => SimfTokens.inkMuted,
      };
}
