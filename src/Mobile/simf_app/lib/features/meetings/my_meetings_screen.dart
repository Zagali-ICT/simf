import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' hide TextDirection;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/my_meetings_repository.dart';

/// One formatter for the meeting timestamps (hoisted off the build path).
final DateFormat _dateTimeFormat = DateFormat('d MMM · hh:mm a');

/// D-479 (#11 follow-up) — اجتماعاتي · My meetings (`/my-meetings`), reached from
/// My Area. **Read-only** (delegation meeting creation/management lives on the
/// Control Panel; speaker meetings are requested from a speaker's profile). One
/// read (`GET /app/my-meetings`, approved-only) lists the user's speaker +
/// delegation meeting requests, newest first, each with a status pill.
class MyMeetingsScreen extends ConsumerStatefulWidget {
  const MyMeetingsScreen({super.key});

  @override
  ConsumerState<MyMeetingsScreen> createState() => _MyMeetingsScreenState();
}

class _MyMeetingsScreenState extends ConsumerState<MyMeetingsScreen> {
  bool _loading = true;
  bool _error = false;
  List<MyMeetingItem> _items = const <MyMeetingItem>[];

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
      final items = await ref.read(myMeetingsRepositoryProvider).getMyMeetings();
      if (!mounted) {
        return;
      }
      setState(() {
        _items = items;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _error = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.myMeetingsTitle,
      onBack: () => ksaBackOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return KsaErrorState(
        message: l10n.myMeetingsError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_items.isEmpty) {
      return KsaEmptyState(
        icon: Icons.event_busy_outlined,
        message: l10n.myMeetingsEmpty,
      );
    }
    final isArabic = l10n.isArabic;
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        for (final item in _items)
          Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space3),
            child: _MeetingCard(item: item, isArabic: isArabic, l10n: l10n),
          ),
      ],
    );
  }
}

/// One meeting row: the kind label, the counterparty + subject, a status pill,
/// and the slot (or submitted) time.
class _MeetingCard extends StatelessWidget {
  const _MeetingCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
  });

  final MyMeetingItem item;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final kindLabel = item.kind == MyMeetingKind.delegation
        ? l10n.myMeetingKindDelegation
        : l10n.myMeetingKindSpeaker;
    final when = (item.slotStartUtc ?? item.createdAt).toLocal();
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    kindLabel,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontSize: SimfTokens.textSm,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                _StatusPill(status: item.status, l10n: l10n),
              ],
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              item.localizedTitle(isArabic),
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textLg,
                fontWeight: FontWeight.w600,
              ),
            ),
            if (item.subject.trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space1),
              Text(
                item.subject,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textSm,
                  height: 1.4,
                ),
              ),
            ],
            const SizedBox(height: SimfTokens.space2),
            Text(
              _dateTimeFormat.format(when),
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: SimfTokens.timestampMuted,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A status pill: gold (pending), green (accepted), red (rejected).
class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.status, required this.l10n});

  final MyMeetingStatus status;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final (color, label) = switch (status) {
      MyMeetingStatus.accepted => (
          SimfTokens.success,
          l10n.myMeetingStatusAccepted,
        ),
      MyMeetingStatus.rejected => (
          SimfTokens.danger,
          l10n.myMeetingStatusRejected,
        ),
      MyMeetingStatus.pending => (
          SimfTokens.accent,
          l10n.myMeetingStatusPending,
        ),
    };
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: color, width: SimfTokens.hairline),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: SimfTokens.textXs,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
