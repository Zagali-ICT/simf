import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:share_plus/share_plus.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'data/myarea_models.dart';
import 'data/myarea_repository.dart';

/// Page 014 — منطقتي · My Area (personal dashboard, #14, `/my-area`).
///
/// A signed-in attendee's hub: identity card, two counters, today's merged
/// schedule, and share actions. An **Approved** user loads
/// `GET /app/account/dashboard`; a signed-in **pending/rejected** user (effective
/// Guest) gets the limited card from the cached identity **without** calling the
/// dashboard (it is Approved-only and would 403 — Page_014 L-5). Share fetches the
/// raw `.vcf`/`.ics` exports and hands them to the native share sheet. The UI is
/// interim; the final visuals come from SIMF-VID-001.
class MyAreaScreen extends ConsumerStatefulWidget {
  const MyAreaScreen({super.key});

  @override
  ConsumerState<MyAreaScreen> createState() => _MyAreaScreenState();
}

class _MyAreaScreenState extends ConsumerState<MyAreaScreen> {
  bool _loading = true;
  bool _error = false;
  MyAreaDashboard? _dashboard;

  @override
  void initState() {
    super.initState();
    final user = _currentUser;
    if (user != null &&
        user.registrationStatus == RegistrationStatus.approved) {
      unawaited(_load());
    } else {
      // Pending / rejected: render the limited card from cache; no dashboard
      // call (it is Approved-only and would 403 — L-5).
      _loading = false;
    }
  }

  CurrentUser? get _currentUser {
    final auth = ref.read(authControllerProvider);
    return auth is AuthStateSignedIn ? auth.session.user : null;
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final dashboard = await ref.read(myAreaRepositoryProvider).getDashboard();
      if (!mounted) {
        return;
      }
      setState(() {
        _dashboard = dashboard;
        _loading = false;
      });
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        // 403 = signed-in but not Approved (edge): fall back to the limited
        // card from cache (L-5). Any other failure shows the retry surface.
        _error = e.httpStatus != 403;
        _dashboard = null;
        _loading = false;
      });
    }
  }

  Future<void> _shareContact() => _share(
        () => ref.read(myAreaRepositoryProvider).getContactCardVcf(),
        'simf.vcf',
        'text/vcard',
      );

  Future<void> _shareCalendar() => _share(
        () => ref.read(myAreaRepositoryProvider).getCalendarIcs(),
        'simf.ics',
        'text/calendar',
      );

  Future<void> _share(
    Future<String> Function() fetch,
    String filename,
    String mimeType,
  ) async {
    try {
      final content = await fetch();
      // Write to a real temp path (no path_provider needed) and share that file
      // so the OS offers add-to-calendar / add-contact rather than plain text.
      final dir = await Directory.systemTemp.createTemp('simf_share');
      final file = File('${dir.path}/$filename');
      await file.writeAsString(content);
      await Share.shareXFiles(<XFile>[XFile(file.path, mimeType: mimeType)]);
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).shareFailed)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.myAreaTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _buildErrorState(l10n);
    }
    final dashboard = _dashboard;
    if (dashboard == null) {
      return _buildLimited(l10n);
    }
    return _buildDashboard(l10n, dashboard);
  }

  Widget _buildErrorState(AppL10n l10n) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(l10n.myAreaError, textAlign: TextAlign.center),
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

  /// The signed-in-but-pending view: the identity card from the cached account
  /// plus an under-review note. No counters / schedule / badge / share (L-5).
  Widget _buildLimited(AppL10n l10n) {
    final user = _currentUser;
    final name = user?.displayName ?? '';
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _ProfileCard(name: name, line: l10n.myAreaPendingNote),
        const SizedBox(height: SimfTokens.space4),
        _UtilityLink(
          label: l10n.accountSettingsLink,
          icon: Icons.settings_outlined,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
      ],
    );
  }

  Widget _buildDashboard(AppL10n l10n, MyAreaDashboard dashboard) {
    final isArabic = l10n.isArabic;
    final identity = dashboard.identity;
    final tier = identity.localizedTier(isArabic);
    final enrolled = l10n.enrolledInSessions(dashboard.counters.bookedSessionsCount);
    final subtitle = tier == null ? enrolled : '$tier · $enrolled';

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _ProfileCard(
          name: identity.localizedName(isArabic),
          line: subtitle,
          reference: identity.qrId == null ? null : '#${identity.qrId}',
          onShare: () => unawaited(_shareContact()),
          shareTooltip: l10n.shareLabel,
        ),
        const SizedBox(height: SimfTokens.space3),
        Row(
          children: <Widget>[
            Expanded(
              child: _ActionTile(
                label: l10n.shareContact,
                icon: Icons.contact_page_outlined,
                onTap: () => unawaited(_shareContact()),
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: _ActionTile(
                label: l10n.shareCalendar,
                icon: Icons.event_available_outlined,
                onTap: () => unawaited(_shareCalendar()),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space3),
        Row(
          children: <Widget>[
            Expanded(
              child: _StatTile(
                value: dashboard.counters.bookedSessionsCount,
                label: l10n.statBookedSessions,
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: _StatTile(
                value: dashboard.counters.meetingsCount,
                label: l10n.statMeetings,
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space5),
        Text(
          l10n.todayScheduleTitle,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: SimfTokens.space2),
        if (dashboard.todaySchedule.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
            child: Text(
              l10n.scheduleEmpty,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
          )
        else
          for (final item in dashboard.todaySchedule)
            _ScheduleRow(
              item: item,
              isArabic: isArabic,
              onTap: item.isSession && item.sessionId != null
                  ? () => context.pushNamed(
                        RouteNames.sessionDetail,
                        pathParameters: <String, String>{
                          'sessionId': item.sessionId!,
                        },
                      )
                  : null,
            ),
        const SizedBox(height: SimfTokens.space5),
        _UtilityLink(
          label: l10n.smartBadgeLink,
          icon: Icons.qr_code_2_outlined,
          onTap: () => context.pushNamed(RouteNames.badge),
        ),
        const SizedBox(height: SimfTokens.space2),
        _UtilityLink(
          label: l10n.accountSettingsLink,
          icon: Icons.settings_outlined,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
      ],
    );
  }
}

/// The identity card — avatar (initials), name, a subtitle line, an optional
/// reference (`#qrId`), and an optional brass Share button.
class _ProfileCard extends StatelessWidget {
  const _ProfileCard({
    required this.name,
    required this.line,
    this.reference,
    this.onShare,
    this.shareTooltip,
  });

  final String name;
  final String line;
  final String? reference;
  final VoidCallback? onShare;
  final String? shareTooltip;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          children: <Widget>[
            CircleAvatar(
              radius: 28,
              backgroundColor: SimfTokens.accent,
              child: Text(
                _initials(name),
                style: const TextStyle(
                  color: SimfTokens.navy,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    name,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    line,
                    style: const TextStyle(
                      color: SimfTokens.inkMuted,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                  if (reference != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      reference!,
                      style: const TextStyle(
                        color: SimfTokens.accent,
                        fontSize: SimfTokens.textXs,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            if (onShare != null)
              IconButton(
                tooltip: shareTooltip,
                onPressed: onShare,
                icon: const Icon(Icons.ios_share, color: SimfTokens.accent),
              ),
          ],
        ),
      ),
    );
  }

  static String _initials(String name) {
    final parts =
        name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) {
      return '–';
    }
    if (parts.length == 1) {
      return parts.first.characters.first.toUpperCase();
    }
    return (parts.first.characters.first + parts.last.characters.first)
        .toUpperCase();
  }
}

/// A square-ish action tile (icon + label) used for the two share affordances.
class _ActionTile extends StatelessWidget {
  const _ActionTile({
    required this.label,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(icon, size: 18, color: SimfTokens.accent),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  label,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: SimfTokens.textSm),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A stat tile — a big number over a label.
class _StatTile extends StatelessWidget {
  const _StatTile({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
        child: Column(
          children: <Widget>[
            Text(
              '$value',
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXl,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(
              label,
              style: const TextStyle(
                color: SimfTokens.inkMuted,
                fontSize: SimfTokens.textXs,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One schedule row — time · title (+ optional hall) · kind icon. Session rows
/// are tappable (→ session detail); meeting rows are not (no detail page yet).
class _ScheduleRow extends StatelessWidget {
  const _ScheduleRow({
    required this.item,
    required this.isArabic,
    this.onTap,
  });

  final MyAreaScheduleItem item;
  final bool isArabic;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final time = DateFormat('HH:mm').format(item.startUtc.toLocal());
    final hall = item.localizedHall(isArabic);
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            children: <Widget>[
              SizedBox(
                width: 44,
                child: Text(
                  time,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      item.localizedTitle(isArabic),
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    if (hall != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        hall,
                        style: const TextStyle(
                          color: SimfTokens.inkMuted,
                          fontSize: SimfTokens.textXs,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              Icon(
                item.isSession ? Icons.event_note_outlined : Icons.people_outline,
                size: 18,
                color: SimfTokens.inkMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A full-width utility link row (label + chevron).
class _UtilityLink extends StatelessWidget {
  const _UtilityLink({
    required this.label,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        leading: Icon(icon, color: SimfTokens.accent),
        title: Text(label),
        trailing: const Icon(Icons.chevron_right, color: SimfTokens.accent),
        onTap: onTap,
      ),
    );
  }
}
