import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_app/features/content/widgets/terms_body.dart';
import 'package:simf_app/features/content/widgets/terms_header_bar.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Terms & conditions — route: `RouteNames.terms` · Figma 505:1553
///
/// Contract: a read-only view over the anonymous `GET /app/content/terms`. Two
/// modes (Page_009 L-2, D-367 / D-375): standalone read, where موافق simply
/// leaves the page, and in-flow consent, where the explicit **موافق** tap IS
/// the consent (client-side only, D8 — `pop(true)`) and the back chevron
/// declines via `pop(false)`.
class TermsScreen extends ConsumerWidget {
  const TermsScreen({super.key, this.requireConsent = false});

  final bool requireConsent;

  void _accept(BuildContext context) {
    // Client-side consent only (D8) — hand control back to the calling flow.
    // Standalone (no gate): موافق simply leaves the page, same as the chevron.
    if (context.canPop()) {
      context.pop(requireConsent ? true : null);
    } else {
      context.go('/');
    }
  }

  void _back(BuildContext context) {
    // In consent mode the chevron declines (the caller receives false).
    if (context.canPop()) {
      context.pop(requireConsent ? false : null);
    } else {
      context.go('/');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          // Decorative diagonal sweep (Figma 505:1555, top-right area).
          Positioned(
            top: -180,
            right: -40,
            child: Transform.rotate(
              angle: 0.4936, // 28.28°
              child: Container(
                width: SimfTokens.sweepBlockWidth,
                height: SimfTokens.sweepBlockHeight,
                decoration: BoxDecoration(
                  color: SimfTokens.surfaceTint,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSheet),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                TermsHeaderBar(
                  title: l10n.termsTitle,
                  onBack: () => _back(context),
                ),
                Expanded(
                  child: SimfPullToRefresh(
                    onRefresh: () =>
                        refreshAsync(ref, termsBlockProvider.future),
                    child: _buildBody(context, ref, l10n),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody(BuildContext context, WidgetRef ref, AppL10n l10n) {
    // A retry re-runs the provider; `refreshAsync` is for the PULL, whose
    // future the RefreshIndicator awaits.
    void retry() => ref.invalidate(termsBlockProvider);

    return ref.watch(termsBlockProvider).when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
          error: (error, _) => SimfPullableHost(
            child: SimfErrorState(
              message: error is ApiFailure
                  ? error.localizedMessage(l10n)
                  : l10n.errorGenericBody,
              retryLabel: l10n.retryLabel,
              onRetry: retry,
            ),
          ),
          // Null is the empty state (see [termsBlockProvider]): a missing or
          // inactive block is empty, not broken — but the design still offers a
          // retry, so the shared error surface, which carries one, is the right
          // widget here and not the icon-only SimfEmptyState.
          data: (block) => block == null
              ? SimfPullableHost(
                  child: SimfErrorState(
                    message: l10n.termsEmpty,
                    retryLabel: l10n.retryLabel,
                    onRetry: retry,
                  ),
                )
              // Each non-empty body line renders as one bullet card (Figma
              // list items).
              : TermsBody(
                  bullets: block.bullets(isArabic: l10n.isArabic),
                  headingLabel: l10n.termsImportantInfoTitle,
                  acceptLabel: l10n.termsAcceptButton,
                  onAccept: () => _accept(context),
                ),
        );
  }
}
