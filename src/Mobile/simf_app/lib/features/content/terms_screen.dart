import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/content_models.dart';
import 'data/content_repository.dart';

const Color _sweepTint = Color(0x0AFFFFFF);

/// Page 009 — الشروط والأحكام · Terms & conditions. The KSA-Project Figma
/// design (node 505:1553 — D-367): navy surface + sweep, custom header, the
/// معلومات هامة لزوار الملتقى heading, the terms rendered as gold-hairline
/// bullet cards, and (consent mode) the gold موافق button. The previous
/// screen is parked in `_legacy_mockup/`.
///
/// Contract: a read-only view over the anonymous `GET /app/content/terms`.
/// Two modes (Page_009 L-2): standalone read (no gate) and in-flow consent —
/// per the design the interim checkbox is gone and the explicit **موافق** tap
/// IS the consent (still client-side only, D8 — control returns to the caller
/// via `pop(true)`; the back chevron declines via `pop(false)`). Each
/// non-empty line of the server body renders as one bullet card; a 404 is the
/// empty state, transport/5xx is the error state with retry (L-6). Guest+.
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
    if (context.canPop()) {
      context.pop(true);
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
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: _sweepTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                // Header band (Figma 505:1558): chevron left, centred title.
                SizedBox(
                  height: 56,
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding: const EdgeInsets.only(left: 8),
                          child: IconButton(
                            onPressed: _back,
                            icon: const Icon(
                              Icons.arrow_back_ios_new,
                              color: Colors.white,
                              size: 20,
                              textDirection: TextDirection.ltr,
                            ),
                          ),
                        ),
                      ),
                      Text(
                        l10n.termsTitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 24,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(child: _buildBody(l10n)),
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
      return _buildMessage(l10n, _error!);
    }
    if (_empty) {
      return _buildMessage(l10n, l10n.termsEmpty);
    }
    return _buildContent(l10n);
  }

  Widget _buildContent(AppL10n l10n) {
    final block = _block!;
    final body = block.localizedBody(l10n.isArabic);
    // Each non-empty body line renders as one bullet card (Figma list items).
    final items = body
        .split('\n')
        .map((line) => line.trim())
        .where((line) => line.isNotEmpty)
        .toList();
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  const SizedBox(height: 16),
                  Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: Text(
                      l10n.termsImportantInfoTitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (block.lastUpdatedAt != null) ...<Widget>[
                    const SizedBox(height: 8),
                    Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: Text(
                        l10n.termsLastUpdated(
                          _formatDate(block.lastUpdatedAt!),
                        ),
                        style: const TextStyle(
                          color: SimfTokens.txtTertiary,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(height: 16),
                  for (final item in items) ...<Widget>[
                    _BulletCard(text: item),
                    const SizedBox(height: 16),
                  ],
                  const SizedBox(height: 8),
                ],
              ),
            ),
          ),
        ),
        if (widget.requireConsent)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
            child: SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: _accept,
                child: Text(
                  l10n.termsAcceptButton,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }

  Widget _buildMessage(AppL10n l10n, String message) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtSecondary),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(
              onPressed: _load,
              child: Text(l10n.retryLabel),
            ),
          ],
        ),
      ),
    );
  }

  static String _formatDate(DateTime date) {
    final local = date.toLocal();
    final year = local.year.toString().padLeft(4, '0');
    final month = local.month.toString().padLeft(2, '0');
    final day = local.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }
}

/// One gold-hairline bullet card (Figma 505:1639): the gold • at the inline
/// start, the term text in `beigeBorder`.
class _BulletCard extends StatelessWidget {
  const _BulletCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        border: Border.all(color: SimfTokens.accent, width: 0.4),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(start: 4, end: 12),
            child: Text(
              '•',
              style: TextStyle(color: SimfTokens.accent, fontSize: 16),
            ),
          ),
          Expanded(
            child: SelectableText(
              text,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: 14,
                height: 1.5,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
