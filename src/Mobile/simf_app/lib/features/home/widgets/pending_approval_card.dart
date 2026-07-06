import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';

/// The home block for a signed-in but **not-yet-approved** account (D-666): the
/// "awaiting approval" note plus the two actions a true guest never gets —
/// **re-check** (re-fetches the account status; the moment it is approved the
/// auth-state change rebuilds Home into the full experience) and **registration
/// status** (opens the status gate, Page 011). It replaces the guest "sign in"
/// CTA, which is wrong for an already-logged-in user.
class PendingApprovalCard extends ConsumerStatefulWidget {
  const PendingApprovalCard({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  ConsumerState<PendingApprovalCard> createState() =>
      _PendingApprovalCardState();
}

class _PendingApprovalCardState extends ConsumerState<PendingApprovalCard> {
  bool _refreshing = false;

  Future<void> _refresh() async {
    final l10n = widget.l10n;
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _refreshing = true);
    try {
      final user =
          await ref.read(authControllerProvider.notifier).refreshCurrentUser();
      if (!mounted) {
        return;
      }
      // Approved → the auth-state change rebuilds Home into the full experience,
      // so only surface a message when it is still not approved.
      if (user.registrationStatus != RegistrationStatus.approved) {
        messenger.showSnackBar(
          SnackBar(content: Text(l10n.statusStillPending)),
        );
      }
    } on AuthFailure {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(SnackBar(content: Text(l10n.regStatusError)));
    } finally {
      if (mounted) {
        setState(() => _refreshing = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space3),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent, width: 0.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              const Icon(
                Icons.hourglass_top,
                color: SimfTokens.accent,
                size: 24,
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Text(
                  l10n.homePendingApprovalNote,
                  textAlign: TextAlign.start,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textMd,
                    height: 1.5,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _refreshing ? null : () => unawaited(_refresh()),
                  icon: _refreshing
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: SimfTokens.accent,
                          ),
                        )
                      : const Icon(Icons.refresh, size: 18),
                  label: Text(l10n.reCheckButton),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: FilledButton(
                  onPressed: () =>
                      context.pushNamed(RouteNames.registrationStatus),
                  child: Text(l10n.registrationStatusButton),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
