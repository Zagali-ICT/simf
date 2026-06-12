import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../core/sharing/content_sharer.dart';
import 'data/myarea_models.dart';
import 'data/myarea_repository.dart';

/// Page 014 — منطقتي · My Area (#14, `/my-area`), rebuilt to the KSA Wave-2
/// frame **512:1780 "My Place"** (owner-picked over 213:963) on the shared
/// shell.
///
/// Behaviour contract unchanged from the mockup build: an **Approved** user
/// loads `GET /app/account/dashboard`; a signed-in pending/rejected user gets
/// the limited cached-identity view without calling it (Approved-only, would
/// 403 — Page_014 L-5); share actions fetch the raw `.vcf`/`.ics` exports for
/// the native share sheet; sign-out (D-373) confirms then revokes.
///
/// Frame mapping: identity card (avatar 64 + name + tier·enrolled line +
/// gold #qrId + the bordered مشاركة contact button), the 2×3 tile grid —
/// **العربية • English** (wired language toggle) / **المظهر • ليلي/نهاري**
/// (visible but DISABLED — no light theme exists, owner decision) /
/// **مشاركة ملفي** (→ the share-my-contact QR screen) / **مشاركة جهة اتصال**
/// (.vcf share) / the two API counters — then جدولي اليوم rows and the
/// المزيد rows (بطاقتي الذكية، اعدادات الحساب + the function-preserving
/// مشاركة جدولي and تسجيل الخروج rows the frame's list does not show).
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
      await shareTextContent(
        content: content,
        filename: filename,
        mimeType: mimeType,
      );
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).shareFailed)),
      );
    }
  }

  void _toggleLanguage() {
    final isArabic = ref.read(localeControllerProvider).languageCode == 'ar';
    unawaited(
      ref
          .read(localeControllerProvider.notifier)
          .setLanguage(isArabic ? 'en' : 'ar'),
    );
  }

  /// D-373 — confirm, then revoke the session via the controller (the
  /// server-side refresh token is revoked too) and land on sign-in.
  Future<void> _confirmSignOut(AppL10n l10n) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.signOutLink),
        content: Text(l10n.signOutConfirmBody),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.cancelLabel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.signOutLink),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }
    await ref.read(authControllerProvider.notifier).signOut();
    if (mounted) {
      context.goNamed(RouteNames.signIn);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.myAreaTitle,
      onBack: () =>
          context.canPop() ? context.pop() : context.goNamed(RouteNames.home),
      tab: SimfTab.profile,
      showSweep: true,
      body: _buildBody(l10n),
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
            Text(
              l10n.myAreaError,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
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
        _IdentityCard(name: name, line: l10n.myAreaPendingNote),
        const SizedBox(height: SimfTokens.space4),
        _MoreRow(
          label: l10n.accountSettingsLink,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
        const SizedBox(height: SimfTokens.space4),
        _MoreRow(
          label: l10n.signOutLink,
          onTap: () => unawaited(_confirmSignOut(l10n)),
        ),
      ],
    );
  }

  Widget _buildDashboard(AppL10n l10n, MyAreaDashboard dashboard) {
    final isArabic = l10n.isArabic;
    final identity = dashboard.identity;
    final tier = identity.localizedTier(isArabic);
    final enrolled =
        l10n.enrolledInSessions(dashboard.counters.bookedSessionsCount);
    final subtitle = tier == null ? enrolled : '$tier · $enrolled';

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _IdentityCard(
          name: identity.localizedName(isArabic),
          line: subtitle,
          reference: identity.qrId == null ? null : '#${identity.qrId}',
          avatarUrl: identity.avatarUrl,
          shareLabel: l10n.shareLabel,
          onShare: () => unawaited(_shareContact()),
        ),
        const SizedBox(height: SimfTokens.space4),
        Row(
          children: <Widget>[
            Expanded(
              child: KsaNavTile(
                label: l10n.languageToggleLabel,
                icon: Icons.language,
                onTap: _toggleLanguage,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Visible but disabled — the app has no light theme yet (owner
            // decision: keep the frame's tile as a locked control).
            Expanded(
              child: KsaNavTile(
                label: l10n.themeToggleTooltip,
                icon: Icons.dark_mode_outlined,
                enabled: false,
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          children: <Widget>[
            Expanded(
              child: KsaNavTile(
                label: l10n.shareMyProfile,
                icon: Icons.person_pin_outlined,
                onTap: () => context.pushNamed(RouteNames.shareMyContact),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: KsaNavTile(
                label: l10n.shareContact,
                icon: Icons.swap_horiz,
                onTap: () => unawaited(_shareContact()),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          children: <Widget>[
            Expanded(
              child: KsaStatTile(
                value: dashboard.counters.meetingsCount,
                label: l10n.statMeetings,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: KsaStatTile(
                value: dashboard.counters.bookedSessionsCount,
                label: l10n.statBookedSessions,
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        KsaSectionHeader(title: l10n.todayScheduleTitle),
        const SizedBox(height: SimfTokens.space3),
        if (dashboard.todaySchedule.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
            child: Text(
              l10n.scheduleEmpty,
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
          )
        else
          for (final item in dashboard.todaySchedule)
            Padding(
              padding: const EdgeInsets.only(bottom: SimfTokens.space3),
              child: _ScheduleRow(
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
            ),
        const SizedBox(height: SimfTokens.space3),
        KsaSectionHeader(title: l10n.moreTitle),
        const SizedBox(height: SimfTokens.space3),
        _MoreRow(
          label: l10n.smartBadgeLink,
          onTap: () => context.pushNamed(RouteNames.badge),
        ),
        const SizedBox(height: SimfTokens.space4),
        _MoreRow(
          label: l10n.accountSettingsLink,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
        const SizedBox(height: SimfTokens.space4),
        // Function-preserving rows the frame's (non-exhaustive) list omits:
        // the calendar export kept from the mockup build + sign-out (D-373).
        _MoreRow(
          label: l10n.shareCalendar,
          onTap: () => unawaited(_shareCalendar()),
        ),
        const SizedBox(height: SimfTokens.space4),
        _MoreRow(
          label: l10n.signOutLink,
          onTap: () => unawaited(_confirmSignOut(l10n)),
        ),
      ],
    );
  }
}

/// The identity card (frame node 512:2047): avatar 64, name + tier·enrolled
/// line + gold reference, and the bordered gold مشاركة button.
class _IdentityCard extends StatelessWidget {
  const _IdentityCard({
    required this.name,
    required this.line,
    this.reference,
    this.avatarUrl,
    this.shareLabel,
    this.onShare,
  });

  final String name;
  final String line;
  final String? reference;
  final String? avatarUrl;
  final String? shareLabel;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent, width: 0.2),
      ),
      child: Row(
        children: <Widget>[
          KsaAvatar(name: name, imageUrl: avatarUrl, size: 64),
          const SizedBox(width: SimfTokens.space3),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  line,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
                if (reference != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    reference!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (onShare != null) ...<Widget>[
            const SizedBox(width: SimfTokens.space2),
            InkWell(
              onTap: onShare,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              child: Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: SimfTokens.navy,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  border: Border.all(color: SimfTokens.accent, width: 0.5),
                ),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Padding(
                    padding: const EdgeInsets.all(SimfTokens.space1),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        const Icon(
                          Icons.share_outlined,
                          size: 18,
                          color: SimfTokens.accent,
                        ),
                        if (shareLabel != null)
                          Text(
                            shareLabel!,
                            style: const TextStyle(
                              color: SimfTokens.accent,
                              fontSize: SimfTokens.textSm,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// One schedule row (frame node 512:2116): bold time at the inline start,
/// the title (+ hall, when present), and the gold star at the inline end.
/// Session rows are tappable (→ session detail); meeting rows are not.
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
    final time = DateFormat('hh:mm a').format(item.startUtc.toLocal());
    final hall = item.localizedHall(isArabic);
    return Material(
      color: SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space4,
          ),
          child: Row(
            children: <Widget>[
              Text(
                time,
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text(
                      item.localizedTitle(isArabic),
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w500,
                        fontSize: SimfTokens.textMd,
                      ),
                    ),
                    if (hall != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        hall,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textXs,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              const Icon(
                Icons.star_rounded,
                size: 20,
                color: SimfTokens.accent,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One المزيد row (frame node 512:2126): the label at the inline start and a
/// white forward chevron at the inline end, on the tile chrome.
class _MoreRow extends StatelessWidget {
  const _MoreRow({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space4,
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w500,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
              ),
              const Icon(Icons.chevron_left, size: 20, color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}
