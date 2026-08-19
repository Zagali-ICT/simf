import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/profile_repository.dart'
    show avatarBustProvider, myProfileProvider, referenceNumberProvider;
import 'package:simf_app/features/myarea/data/liveness.dart'
    show CapturedSelfie;
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/widgets/my_area_dashboard_body.dart';
import 'package:simf_app/features/myarea/widgets/my_area_identity_card.dart';
import 'package:simf_app/features/myarea/widgets/my_area_more_row.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// My Area — منطقتي · route: `RouteNames.myArea` · Figma 213:963
/// Contract: an Approved user loads `GET /app/account/dashboard`; a
/// pending/rejected account must NOT call it (403) and gets the limited
/// cached-identity view instead (Page_014 L-5, D-396).
class MyAreaScreen extends ConsumerStatefulWidget {
  const MyAreaScreen({super.key});

  @override
  ConsumerState<MyAreaScreen> createState() => _MyAreaScreenState();
}

class _MyAreaScreenState extends ConsumerState<MyAreaScreen> {
  CurrentUser? get _currentUser {
    final auth = ref.read(authControllerProvider);
    return auth is AuthStateSignedIn ? auth.session.user : null;
  }

  /// The pull-to-refresh entry point. Drops the cached profile read as well as
  /// re-running the dashboard, so the identity card's reference number
  /// refreshes with the rest of the card instead of being the one stale line on
  /// it — `myProfileProvider` is a SHARED cache, and a pull that does not
  /// invalidate it leaves every selector on the pre-pull row.
  Future<void> _refresh() async {
    ref.invalidate(myProfileProvider);
    await refreshAsync(ref, myAreaDashboardProvider.future);
  }

  /// Runs the guided face-capture / liveness flow (D-404) and uploads the
  /// returned selfie. On success the avatar bust token is bumped so every
  /// avatar on screen (home greeting / badge / this card) refetches the new
  /// photo immediately — the avatar URL is stable, so the token is what forces
  /// the refresh — and the dashboard reloads for the rest of the identity card.
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
      bust.bump();
      ref.invalidate(myAreaDashboardProvider);
    } on ApiFailure catch (e) {
      // Surface the server's actual (user-safe, bilingual) reason instead of a
      // blanket string, so a failure is legible rather than silent.
      if (mounted) {
        final serverMsg = e.localizedMessage(l10n).trim();
        final text = serverMsg.isEmpty ? l10n.avatarUploadFailed : serverMsg;
        messenger.showSnackBar(SnackBar(content: Text(text)));
      }
    } on Object {
      // Any non-ApiFailure error (a raw transport error, etc.) still surfaces
      // a toast rather than failing silently.
      if (mounted) {
        messenger
            .showSnackBar(SnackBar(content: Text(l10n.avatarUploadFailed)));
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
    // BUG-013 — a TRUE guest (no account at all). Checked BEFORE the provider,
    // because the bottom nav switches tabs inside the shell, so the router's
    // auth redirect never runs and a signed-out visitor lands here; the
    // limited view below described an account "under review" that was never
    // submitted, with no way out.
    if (_currentUser == null) {
      return SimfGuestPrompt(
        icon: Icons.person_outline,
        message: l10n.myAreaGuestNote,
        signInLabel: l10n.guestSignInCta,
        createAccountLabel: l10n.signUpButton,
      );
    }
    return ref.watch(myAreaDashboardProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _buildErrorState(l10n),
          // Null is "not Approved" (see [myAreaDashboardProvider]) — the
          // limited card from cache, not an error.
          data: (dashboard) => dashboard == null
              ? _buildLimited(l10n)
              : _buildDashboard(l10n, dashboard, referenceNumber),
        );
  }

  Widget _buildErrorState(AppL10n l10n) {
    // Pull-to-refresh also retries: the error surface is hosted in a scrollable
    // so SimfPullToRefresh's gesture fires even though the content is short.
    return SimfRefreshableMessage(
      onRefresh: _refresh,
      child: SimfErrorState(
        message: l10n.myAreaError,
        retryLabel: l10n.retryLabel,
        onRetry: () => ref.invalidate(myAreaDashboardProvider),
      ),
    );
  }

  /// The signed-in-but-pending view: the identity card from the cached account
  /// plus an under-review note. No counters / schedule / badge / share (L-5).
  Widget _buildLimited(AppL10n l10n) {
    final user = _currentUser;
    final name = user?.displayName ?? '';
    return SimfPullToRefresh(
      onRefresh: _refresh,
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
      onRefresh: _refresh,
      child: MyAreaDashboardBody(
        dashboard: dashboard,
        referenceNumber: referenceNumber,
        onChangeAvatar: () => unawaited(_changeAvatar()),
      ),
    );
  }
}
