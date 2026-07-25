import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../delegations/data/delegations_repository.dart';

/// Bi-Meeting rework — the other-party confirm screen (route `/meeting-confirm`),
/// reached by tapping a "MeetingRequested" notification (deep-link
/// `?requestId=…`). An eligible member of the TARGET delegation confirms the
/// meeting with one tap; on success the meeting summary (both delegations +
/// subject + time) is shown. Eligibility + state are enforced server-side (403 =
/// not the other party, 409 = not awaiting confirmation).
class MeetingConfirmScreen extends ConsumerStatefulWidget {
  const MeetingConfirmScreen({required this.requestId, super.key});

  final String requestId;

  @override
  ConsumerState<MeetingConfirmScreen> createState() =>
      _MeetingConfirmScreenState();
}

class _MeetingConfirmScreenState extends ConsumerState<MeetingConfirmScreen> {
  bool _submitting = false;
  DelegationMeetingSummary? _confirmed;
  String? _error;

  Future<void> _confirm() async {
    if (_submitting) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final summary = await ref
          .read(delegationsRepositoryProvider)
          .confirmMeeting(widget.requestId);
      if (!mounted) {
        return;
      }
      setState(() {
        _confirmed = summary;
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
          _ => l10n.meetingConfirmFailed,
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
          : _confirmed != null
              ? _successView(l10n, _confirmed!)
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
          size: 64,
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
        const Icon(
          Icons.check_circle_outline,
          size: 64,
          color: SimfTokens.accent,
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          l10n.meetingConfirmDone,
          textAlign: TextAlign.center,
          style: SimfTokens.labelInkSemiboldTitle,
        ),
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
            _formatSlot(s.slotStart!.toLocal(), isArabic),
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
    final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
    final hh = hour12.toString().padLeft(2, '0');
    final mm = local.minute.toString().padLeft(2, '0');
    final meridiem = isArabic
        ? (local.hour >= 12 ? 'م' : 'ص')
        : (local.hour >= 12 ? 'PM' : 'AM');
    return '$date · $hh:$mm $meridiem';
  }

  Widget _confirmButton(AppL10n l10n) => Material(
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
}
