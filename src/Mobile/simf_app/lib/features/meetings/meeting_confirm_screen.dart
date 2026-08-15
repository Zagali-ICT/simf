import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart' show RouteNames;
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Confirm meeting — تأكيد الاجتماع · route: [RouteNames.meetingConfirm]
/// Purpose: the OTHER party's one-tap confirm of a bilateral meeting. Data:
/// [delegationsRepositoryProvider].confirmMeeting — `POST
/// /app/delegation-meeting-requests/{id}/confirm`. Figma: no bound node — built
/// on the shared [SimfPageShell] chrome. Perf: two short non-scrolling
/// ListViews; no pagination, one write. Contract: eligibility + state are
/// enforced SERVER-side and the screen only maps the outcome — 403 = not the
/// other party, 409 = not awaiting confirmation, anything else = generic retry.
/// The success summary carries no requester PII (stripped server-side).
///
/// Bi-Meeting rework — the other-party DELEGATION-meeting confirm screen (route
/// `/meeting-confirm`), reached by tapping a "MeetingRequested" notification
/// (deep-link `?requestId=…`). An eligible member of the TARGET delegation
/// confirms — or, since B8, DECLINES — the meeting with one tap; on success the
/// meeting summary (both delegations + subject + time) is shown. Eligibility +
/// state are enforced server-side (403 = not the other party, 409 = not
/// awaiting confirmation).
///
/// A30 — this screen is delegation-only and keyed on a `requestId`. The SPEAKER
/// double-opt-in link lands on the Website's anonymous
/// `/meeting/confirm?token=` page instead; driving a speaker meeting through
/// here is a 403/409 by design, which is why the copy names the delegation.
///
/// Route: `RouteNames.meetingConfirm`.
/// Data: [delegationsRepositoryProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
class MeetingConfirmScreen extends ConsumerStatefulWidget {
  const MeetingConfirmScreen({required this.requestId, super.key});

  final String requestId;

  @override
  ConsumerState<MeetingConfirmScreen> createState() =>
      _MeetingConfirmScreenState();
}

class _MeetingConfirmScreenState extends ConsumerState<MeetingConfirmScreen> {
  bool _submitting = false;
  DelegationMeetingSummary? _outcome;
  // B8 — which action produced [_outcome], so the success view reads
  // "confirmed" or "declined" from the one summary both endpoints return.
  bool _declined = false;
  String? _error;

  Future<void> _confirm() => _submit(decline: false);

  Future<void> _decline() => _submit(decline: true);

  Future<void> _submit({required bool decline}) async {
    if (_submitting) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final repository = ref.read(delegationsRepositoryProvider);
      final summary = decline
          ? await repository.declineMeeting(widget.requestId)
          : await repository.confirmMeeting(widget.requestId);
      if (!mounted) {
        return;
      }
      setState(() {
        _outcome = summary;
        _declined = decline;
        _submitting = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = switch (failure.httpStatus) {
          409 => l10n.meetingConfirmNotAwaiting,
          403 => l10n.delegationNotAllowed,
          _ => decline ? l10n.meetingDeclineFailed : l10n.meetingConfirmFailed,
        };
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.meetingConfirmTitle,
      onBack: () => backOrHome(context),
      body: widget.requestId.isEmpty
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: SimfEmptyState(
                  icon: Icons.event_busy_outlined,
                  message: l10n.meetingConfirmMissing,
                ),
              ),
            )
          : _outcome != null
              ? _successView(l10n, _outcome!)
              : _confirmView(l10n),
    );
  }

  Widget _confirmView(AppL10n l10n) {
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        const Icon(
          Icons.handshake_outlined,
          size: SimfTokens.meetingConfirmScreenSize,
          color: SimfTokens.accent,
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          l10n.meetingConfirmIntro,
          textAlign: TextAlign.center,
          style: SimfTokens.bodyGreyMd,
        ),
        if (_error != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space4),
          Text(
            _error!,
            textAlign: TextAlign.center,
            style: SimfTokens.bodyGreyMd,
          ),
        ],
        const SizedBox(height: SimfTokens.space6),
        _confirmButton(l10n),
        // B8 — the decline action. Same authorisation as confirm; the
        // requester is notified and the hall slot is released server-side.
        const SizedBox(height: SimfTokens.space3),
        _declineButton(l10n),
      ],
    );
  }

  Widget _successView(AppL10n l10n, DelegationMeetingSummary s) {
    final isArabic = l10n.isArabic;
    final parties = '${s.requestingCountry} — ${s.targetCountry}';
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        Icon(
          _declined ? Icons.cancel_outlined : Icons.check_circle_outline,
          size: SimfTokens.meetingConfirmScreenSize,
          color: SimfTokens.accent,
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          _declined ? l10n.meetingDeclineDone : l10n.meetingConfirmDone,
          textAlign: TextAlign.center,
          style: SimfTokens.labelInkSemiboldTitle,
        ),
        if (_declined) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.meetingDeclineIntro,
            textAlign: TextAlign.center,
            style: SimfTokens.bodyGreyMd,
          ),
        ],
        const SizedBox(height: SimfTokens.space4),
        if (parties.trim().isNotEmpty && parties.trim() != '—')
          Text(
            parties,
            textAlign: TextAlign.center,
            style: SimfTokens.labelNavyMediumSm,
          ),
        if (s.subject.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            s.subject,
            textAlign: TextAlign.center,
            style: SimfTokens.bodyGreyMd,
          ),
        ],
        if (s.slotStart != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            _formatSlot(saudiOf(s.slotStart!), isArabic),
            textAlign: TextAlign.center,
            style: SimfTokens.bodyGreyMd,
          ),
        ],
      ],
    );
  }

  // "2026-11-24 · 10:00 ص" — a compact local date + 12-hour time.
  String _formatSlot(DateTime local, bool isArabic) {
    final date = '${local.year.toString().padLeft(4, '0')}-'
        '${local.month.toString().padLeft(2, '0')}-'
        '${local.day.toString().padLeft(2, '0')}';
    final time = formatDateTime12h(local, isArabic: isArabic);
    return '$date · $time';
  }

  Widget _confirmButton(AppL10n l10n) => Material(
        key: const ValueKey<String>('delegation-meeting-confirm'),
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          onTap: _submitting ? null : () => unawaited(_confirm()),
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Container(
            height: SimfTokens.controlHeight,
            alignment: Alignment.center,
            child: Text(
              _submitting ? l10n.loadingLabel : l10n.meetingConfirmButton,
              style: SimfTokens.labelWhiteBoldLg,
            ),
          ),
        ),
      );

  // B8 — the secondary (outlined) decline action, so declining never reads as
  // the primary path but is always reachable.
  Widget _declineButton(AppL10n l10n) => Material(
        key: const ValueKey<String>('delegation-meeting-decline'),
        color: SimfTokens.surface,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: InkWell(
          onTap: _submitting ? null : () => unawaited(_decline()),
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Container(
            height: SimfTokens.controlHeight,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              borderRadius: SimfTokens.borderRadiusSmall,
              border: Border.fromBorderSide(
                BorderSide(color: SimfTokens.beigeBorder),
              ),
            ),
            child: Text(
              l10n.meetingDeclineButton,
              style: SimfTokens.labelNavyMediumSm,
            ),
          ),
        ),
      );
}
