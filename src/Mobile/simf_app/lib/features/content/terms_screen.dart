import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
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
class TermsScreen extends ConsumerStatefulWidget {
  const TermsScreen({super.key, this.requireConsent = false});

  final bool requireConsent;

  @override
  ConsumerState<TermsScreen> createState() => _TermsScreenState();
}

class _TermsScreenState extends ConsumerState<TermsScreen> {
  bool _loading = true;
  bool _empty = false;
  String? _error;
  ContentBlock? _block;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _empty = false;
    });
    try {
      final block = await ref
          .read(contentRepositoryProvider)
          .getContentBlock(ContentRepository.termsKey);
      if (!mounted) {
        return;
      }
      setState(() {
        _block = block;
        _empty = !block.hasBody;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        // A missing/inactive key is an empty state, not an error (L-6).
        if (failure.httpStatus == 404) {
          _empty = true;
        } else {
          _error = failure.message;
        }
        _loading = false;
      });
    }
  }

  void _accept() {
    // Client-side consent only (D8) — hand control back to the calling flow.
    // Standalone (no gate): موافق simply leaves the page, same as the chevron.
    if (context.canPop()) {
      context.pop(widget.requireConsent ? true : null);
    } else {
      context.go('/');
    }
  }

  void _back() {
    // In consent mode the chevron declines (the caller receives false).
    if (context.canPop()) {
      context.pop(widget.requireConsent ? false : null);
    } else {
      context.go('/');
    }
  }

  @override
  Widget build(BuildContext context) {
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
                width: SimfTokens.termsScreenWidth,
                height: SimfTokens.termsScreenHeightMd,
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
                            onPressed: _back,
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
                    onRefresh: _load,
                    child: _buildBody(l10n),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_error != null) {
      return SimfPullableHost(
        child: SimfErrorState(
          message: _error!,
          retryLabel: l10n.retryLabel,
          onRetry: _load,
        ),
      );
    }
    if (_empty) {
      // A missing/inactive block is empty, not broken — but the design still
      // offers a retry here, so the shared error surface (which carries the
      // retry) is the right shared widget, not the icon-only SimfEmptyState.
      return SimfPullableHost(
        child: SimfErrorState(
          message: l10n.termsEmpty,
          retryLabel: l10n.retryLabel,
          onRetry: _load,
        ),
      );
    }
    return _buildContent(l10n);
  }

  Widget _buildContent(AppL10n l10n) {
    // Each non-empty body line renders as one bullet card (Figma list items).
    final items = _block!.bullets(isArabic: l10n.isArabic);
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
              onPressed: _accept,
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
