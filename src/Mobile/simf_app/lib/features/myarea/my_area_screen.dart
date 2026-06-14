import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/sharing/content_sharer.dart';
import '../profile/data/profile_repository.dart' show referenceNumberProvider;
import 'data/myarea_models.dart';
import 'data/myarea_repository.dart';
import 'identity_verification_screen.dart';

/// One 12-hour formatter for the schedule rows (hoisted off the build path).
final DateFormat _timeFormat = DateFormat('hh:mm a');

/// Page 014 — منطقتي · My Area (#14, `/my-area`), rebuilt to the KSA Wave-2
/// frame **213:963** (owner re-pick, D-396; the earlier build used 512:1780)
/// on the shared shell.
///
/// Behaviour contract unchanged from the mockup build: an **Approved** user
/// loads `GET /app/account/dashboard`; a signed-in pending/rejected user gets
/// the limited cached-identity view without calling it (Approved-only, would
/// 403 — Page_014 L-5); the contact share fetches the raw `.vcf` export for
/// the native share sheet.
///
/// Frame mapping (213:963): identity card (avatar 64 + name + tier·enrolled
/// line + gold #qrId + the bordered مشاركة contact button), the two share
/// actions (**مشاركة ملفي** → the share-my-contact QR screen / **مشاركة جهة
/// اتصال** → .vcf share), the **الإحصائيات** section (two stat tiles —
/// جلسات محفوظة = booked sessions; the second keeps the real مقابلات مؤكدة
/// count since الأرشيف has no API counter, D-396), جدولي اليوم rows, then the
/// المزيد rows (بطاقتي الذكية، اعدادات الحساب). The language toggle, the
/// (inert) theme tile, the calendar export and sign-out moved to the shell's
/// side drawer (D-396).
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
    if (user != null && user.isApproved) {
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

  /// Runs the guided face-capture / liveness flow (D-404) and uploads the
  /// returned selfie. On success the image cache is cleared (the avatar URL can
  /// be stable) and the dashboard reloads so the new photo shows.
  Future<void> _changeAvatar() async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final selfie = await context
        .pushNamed<CapturedSelfie>(RouteNames.identityVerification);
    if (selfie == null || !mounted) {
      return;
    }
    try {
      await ref.read(myAreaRepositoryProvider).uploadAvatar(
            bytes: selfie.bytes,
            filename: selfie.filename,
          );
      if (!mounted) {
        return;
      }
      PaintingBinding.instance.imageCache.clear();
      PaintingBinding.instance.imageCache.clearLiveImages();
      await _load();
    } on ApiFailure {
      messenger.showSnackBar(SnackBar(content: Text(l10n.avatarUploadFailed)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // The ID line is the reference number (SIMF-2026-…), not the qrId.
    final referenceNumber = ref.watch(referenceNumberProvider).asData?.value;
    return KsaPage(
      title: l10n.myAreaTitle,
      onBack: () => ksaBackOrHome(context),
      tab: SimfTab.profile,
      showSweep: true,
      body: _buildBody(l10n, referenceNumber),
    );
  }

  Widget _buildBody(AppL10n l10n, String? referenceNumber) {
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
    return _buildDashboard(l10n, dashboard, referenceNumber);
  }

  Widget _buildErrorState(AppL10n l10n) {
    return KsaErrorState(
      message: l10n.myAreaError,
      retryLabel: l10n.retryLabel,
      onRetry: () => unawaited(_load()),
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
        // Sign-out lives in the shell's side drawer now (D-396).
      ],
    );
  }

  Widget _buildDashboard(
    AppL10n l10n,
    MyAreaDashboard dashboard,
    String? referenceNumber,
  ) {
    final isArabic = l10n.isArabic;
    final identity = dashboard.identity;
    final tier = identity.localizedTier(isArabic);
    final enrolled =
        l10n.enrolledInSessions(dashboard.counters.bookedSessionsCount);
    final subtitle = tier == null ? enrolled : '$tier · $enrolled';
    // The reference number (SIMF-2026-…) is the displayed ID; fall back to the
    // qrId only until the reference loads.
    final reference = referenceNumber != null
        ? '#$referenceNumber'
        : (identity.qrId == null ? null : '#${identity.qrId}');

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _IdentityCard(
          name: identity.localizedName(isArabic),
          line: subtitle,
          reference: reference,
          avatarUrl: identity.avatarUrl,
          shareLabel: l10n.shareLabel,
          onShare: () => unawaited(_shareContact()),
          onAvatarTap: () => unawaited(_changeAvatar()),
          avatarTooltip: l10n.avatarChangeTooltip,
        ),
        const SizedBox(height: SimfTokens.space4),
        // Frame 758:1283 — two horizontal share-action pills (h48, exact
        // iconify glyphs), NOT the tall vertical nav tiles. Language / theme
        // moved to the shell's side drawer (D-396).
        Row(
          children: <Widget>[
            Expanded(
              child: _ShareTile(
                label: l10n.shareMyProfile,
                iconAsset: 'assets/icons/share_profile.svg',
                onTap: () => context.pushNamed(RouteNames.shareMyContact),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _ShareTile(
                label: l10n.shareContact,
                iconAsset: 'assets/icons/share_contact.svg',
                onTap: () => unawaited(_shareContact()),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        // الإحصائيات (frame 213:963). الأرشيف has no API counter, so the right
        // tile keeps the real مقابلات مؤكدة count (D-396); جلسات محفوظة = booked.
        KsaSectionHeader(title: l10n.statisticsTitle),
        const SizedBox(height: SimfTokens.space3),
        KsaTileRow(
          children: <Widget>[
            KsaStatTile(
              value: dashboard.counters.meetingsCount,
              label: l10n.statMeetings,
            ),
            KsaStatTile(
              value: dashboard.counters.bookedSessionsCount,
              label: l10n.statBookedSessions,
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
        // Calendar export + sign-out moved to the shell's side drawer (D-396),
        // matching frame 213:963 which ends المزيد at اعدادات الحساب.
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
    this.onAvatarTap,
    this.avatarTooltip,
  });

  final String name;
  final String line;
  final String? reference;
  final String? avatarUrl;
  final String? shareLabel;
  final VoidCallback? onShare;
  final VoidCallback? onAvatarTap;
  final String? avatarTooltip;

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
          _TappableAvatar(
            name: name,
            imageUrl: avatarUrl,
            onTap: onAvatarTap,
            tooltip: avatarTooltip,
          ),
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
                    fontSize: SimfTokens.textTitle,
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

/// The profile avatar with a tap-to-change affordance (frame 213:963): the gold
/// rounded avatar plus a small camera badge at the corner. A null [onTap] (the
/// limited/pending view) renders a plain avatar with no camera badge.
class _TappableAvatar extends StatelessWidget {
  const _TappableAvatar({
    required this.name,
    this.imageUrl,
    this.onTap,
    this.tooltip,
  });

  final String name;
  final String? imageUrl;
  final VoidCallback? onTap;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final avatar = KsaAvatar(name: name, imageUrl: imageUrl, size: 64);
    if (onTap == null) {
      return avatar;
    }
    return Semantics(
      button: true,
      label: tooltip,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Stack(
          clipBehavior: Clip.none,
          children: <Widget>[
            avatar,
            Positioned.directional(
              textDirection: Directionality.of(context),
              end: -2,
              bottom: -2,
              child: Container(
                padding: const EdgeInsets.all(SimfTokens.space1),
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  shape: BoxShape.circle,
                  border: Border.all(color: SimfTokens.navyDeep, width: 1.5),
                ),
                child: const Icon(
                  Icons.photo_camera_outlined,
                  size: 12,
                  color: SimfTokens.navy,
                ),
              ),
            ),
          ],
        ),
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
    final time = _timeFormat.format(item.startUtc.toLocal());
    final hall = item.localizedHall(isArabic);
    return KsaCard(
      onTap: onTap,
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
    return KsaCard(
      onTap: onTap,
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
              const SimfSvgIcon(
                'assets/icons/ic_back.svg',
                size: 20,
                color: Colors.white,
              ),
            ],
          ),
      ),
    );
  }
}

/// One horizontal share-action pill (frame 758:1283): the gold iconify glyph
/// beside its label on the shared card chrome, fixed to the frame's 48-px row.
class _ShareTile extends StatelessWidget {
  const _ShareTile({
    required this.label,
    required this.iconAsset,
    required this.onTap,
  });

  final String label;
  final String iconAsset;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      child: SizedBox(
        height: 48,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SimfSvgIcon(iconAsset, size: 20, color: SimfTokens.accent),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
