import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../account/data/profile_repository.dart'
    show avatarBustProvider, referenceNumberProvider;
import 'data/myarea_models.dart';
import 'data/myarea_repository.dart';
import 'identity_verification_screen.dart';
import 'widgets/my_area_dashboard_body.dart';
import 'widgets/my_area_identity_card.dart';
import 'widgets/my_area_rows.dart';

/// Page 014 — منطقتي · My Area (#14, `/my-area`), rebuilt to the KSA Wave-2
/// frame **213:963** (owner re-pick, D-396; the earlier build used 512:1780)
/// on the shared shell.
///
/// Behaviour contract unchanged from the mockup build: an **Approved** user
/// loads `GET /app/account/dashboard`; a signed-in pending/rejected user gets
/// the limited cached-identity view without calling it (Approved-only, would
/// 403 — Page_014 L-5).
///
/// Frame mapping (213:963): identity card (avatar 64 + name + tier·enrolled
/// line + gold #qrId + the bordered مشاركة contact button), the two share
/// actions (**مشاركة ملفي** and **مشاركة جهة اتصال**) which both open the
/// in-app share-my-contact QR screen (#21 — مشاركة جهة اتصال was a native
/// `.vcf` share sheet), the **الإحصائيات** section (two stat tiles —
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

  /// Runs the guided face-capture / liveness flow (D-404) and uploads the
  /// returned selfie. On success the avatar bust token is bumped so every avatar
  /// on screen (home greeting / badge / this card) refetches the new photo
  /// immediately — the avatar URL is stable, so the token is what forces the
  /// refresh — and the dashboard reloads for the rest of the identity card.
  Future<void> _changeAvatar() async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    // Capture the provider references BEFORE the async gap. The guided liveness
    // takes several seconds, during which a token proactive-refresh can churn
    // go_router and dispose THIS page's State. If the upload were gated on
    // `mounted` after the capture (as it was), that disposal silently dropped
    // the new avatar — it never reached the server. Reading the repo + bust
    // up-front lets the upload finish regardless of this widget's lifecycle;
    // only the on-screen feedback stays gated on `mounted`.
    final repo = ref.read(myAreaRepositoryProvider);
    final bust = ref.read(avatarBustProvider.notifier);
    final selfie = await context.pushNamed<CapturedSelfie>(
      RouteNames.identityVerification,
      extra: true, // showConfirmation
    );
    if (selfie == null) {
      return;
    }
    try {
      await repo.uploadAvatar(bytes: selfie.bytes, filename: selfie.filename);
      bust.state++;
      if (mounted) {
        await _load();
      }
    } on ApiFailure catch (e) {
      // Surface the server's actual (user-safe, bilingual) reason instead of a
      // blanket string, so a failure is legible rather than silent.
      if (mounted) {
        final serverMsg = e.message.trim();
        final text = serverMsg.isEmpty ? l10n.avatarUploadFailed : serverMsg;
        messenger.showSnackBar(SnackBar(content: Text(text)));
      }
    } on Object {
      // Any non-ApiFailure error (a raw transport error, etc.) still surfaces
      // a toast rather than failing silently.
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text(l10n.avatarUploadFailed)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // The ID line is the reference number (SIMF-2026-…), not the qrId.
    final referenceNumber = ref.watch(referenceNumberProvider).asData?.value;
    return SimfPageShell(
      title: l10n.myAreaTitle,
      onBack: () => backOrHome(context),
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
    // Pull-to-refresh also retries: the error surface is hosted in a scrollable
    // so SimfPullToRefresh's gesture fires even though the content is short.
    return SimfPullToRefresh(
      onRefresh: _load,
      child: SimfPullableHost(
        child: SimfErrorState(
          message: l10n.myAreaError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      ),
    );
  }

  /// The signed-in-but-pending view: the identity card from the cached account
  /// plus an under-review note. No counters / schedule / badge / share (L-5).
  Widget _buildLimited(AppL10n l10n) {
    final user = _currentUser;
    final name = user?.displayName ?? '';
    return SimfPullToRefresh(
      onRefresh: _load,
      child: ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        MyAreaIdentityCard(name: name, line: l10n.myAreaPendingNote),
        const SizedBox(height: SimfTokens.space4),
        MyAreaMoreRow(
          label: l10n.moreTitle,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
        // Sign-out lives in the shell's side drawer now (D-396).
      ],
      ),
    );
  }

  Widget _buildDashboard(
    AppL10n l10n,
    MyAreaDashboard dashboard,
    String? referenceNumber,
  ) {
    return SimfPullToRefresh(
      onRefresh: _load,
      child: MyAreaDashboardBody(
        dashboard: dashboard,
        referenceNumber: referenceNumber,
        onChangeAvatar: () => unawaited(_changeAvatar()),
      ),
    );
  }
}
