import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_app/features/content/widgets/terms_bullet_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 009 — الشروط والأحكام · Terms & conditions. The KSA-Project Figma
/// design (node 505:1553 — D-367, fidelity pass D-375): navy surface + sweep,
/// custom header, the معلومات هامة لزوار الملتقى heading, the terms rendered
/// as gold-hairline bullet cards, and the gold موافق button — per the frame
/// the button always shows and there is no last-updated line. The previous
/// screen is parked in `_legacy_mockup/`.
///
/// Contract: a read-only view over the anonymous `GET /app/content/terms`.
/// Two modes (Page_009 L-2): standalone read (موافق simply leaves the page)
/// and in-flow consent — per the design the interim checkbox is gone and the
/// explicit **موافق** tap IS the consent (still client-side only, D8 —
/// control returns to the caller via `pop(true)`; the back chevron declines
/// via `pop(false)`). Each non-empty line of the server body renders as one
/// bullet card; a 404 is the empty state, transport/5xx is the error state
/// with retry (L-6). Guest+.
///
/// Route: `RouteNames.terms`.
/// Data: [termsBlockProvider] over [contentRepositoryProvider].
/// Perf: no list — a single-screen layout.

/// The terms block, or **null for the empty state**.
///
/// Two different things mean "there is nothing to show", and both are empty
/// rather than broken (Page_009 L-6): the key is missing or inactive (a 404),
/// or it exists with no body. Folding them here is what lets the screen read
/// `AsyncValue` directly — three server outcomes collapse into the three
/// branches `when` already has, instead of a fourth `_empty` flag beside them.
///
/// Any other failure propagates, so the error branch shows the server's own
/// message rather than a generic one.
final termsBlockProvider =
    FutureProvider.autoDispose<ContentBlock?>((ref) async {
  try {
    final block = await ref
        .watch(contentRepositoryProvider)
        .getContentBlock(ContentRepository.termsKey);
    return block.hasBody ? block : null;
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});

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
                // Header band (Figma 505:1558): chevron left, centred title.
                SizedBox(
                  height: SimfTokens.termsScreenHeightSm,
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding:
                              const EdgeInsets.only(left: SimfTokens.space2),
                          child: IconButton(
                            onPressed: () => _back(context),
                            tooltip: MaterialLocalizations.of(context)
                                .backButtonTooltip,
                            icon: const Icon(
                              Icons.arrow_back_ios_new,
                              color: SimfTokens.surface,
                              size: SimfTokens.termsScreenSize,
                              textDirection: TextDirection.ltr,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        l10n.termsTitle,
                        style: const TextStyle(
                          color: SimfTokens.surface,
                          fontSize: SimfTokens.text24,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
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
              : _buildContent(context, l10n, block),
        );
  }

  Widget _buildContent(BuildContext context, AppL10n l10n, ContentBlock block) {
    // Each non-empty body line renders as one bullet card (Figma list items).
    final items = block.bullets(isArabic: l10n.isArabic);
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            // Full-width content (owner 2026-06-20): the cards stretch to the
            // page width instead of the old 400-wide centred column.
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                const SizedBox(height: SimfTokens.space4),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: Text(
                    l10n.termsImportantInfoTitle,
                    style: SimfTokens.labelWhiteBoldLg,
                  ),
                ),
                const SizedBox(height: SimfTokens.space4),
                for (final item in items) ...<Widget>[
                  TermsBulletCard(text: item),
                  const SizedBox(height: SimfTokens.space4),
                ],
                const SizedBox(height: SimfTokens.space2),
              ],
            ),
          ),
        ),
        // The frame shows موافق unconditionally (505:1684); standalone it
        // simply leaves the page, in consent mode it returns true (D-375).
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            0,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          child: SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: () => _accept(context),
              child: Text(
                l10n.termsAcceptButton,
                style: SimfTokens.titleBold,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
