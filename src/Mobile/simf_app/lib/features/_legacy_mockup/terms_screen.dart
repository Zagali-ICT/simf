import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../content/data/content_models.dart';
import '../content/data/content_repository.dart';

/// LEGACY — the pre-redesign Page 009 terms screen, parked here when the
/// KSA-Project design landed at the real path (D-367). Never routed; kept
/// compiling until programme close (§6 freeze rules).
///
/// A read-only content view backed by the anonymous `GET /app/content/terms`
/// (the `terms` content key). Two modes (Page_009 L-2), chosen by the caller:
///   - **standalone read** ([requireConsent] = false): body only, no gate;
///   - **in-flow consent** ([requireConsent] = true): a bottom accept gate
///     (checkbox + Accept) whose acceptance is **client-side only** (D8 — no
///     backend write) and hands control back to the calling flow via `pop`.
///
/// A missing/inactive key (404) renders the **empty** state; a transport/5xx
/// failure renders the **error** state with retry (L-6). Reading is open to any
/// privilege (Guest and above), so the route is not auth-gated. The body is
/// shown as plain selectable text in this interim UI; rich HTML/markdown
/// rendering lands with the final design (SIMF-VID-001).
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
  bool _accepted = false;

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

  void _decline() {
    if (context.canPop()) {
      context.pop(false);
    } else {
      context.go('/');
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.termsTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
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
    // .terms frame: a navy surface holding a scrollable .doc box, then the
    // accept gate. The gap matches the mockup's column gap of 12px.
    return Column(
      children: <Widget>[
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space5,
              SimfTokens.space4,
              SimfTokens.space5,
            ),
            child: _buildDocBox(l10n, block, body),
          ),
        ),
        if (widget.requireConsent) _buildConsentGate(l10n),
      ],
    );
  }

  // .terms .doc — white-4% fill, line2 hairline, radius 8.
  Widget _buildDocBox(AppL10n l10n, ContentBlock block, String body) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.surfaceTint,
        border: Border.all(color: SimfTokens.line2),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (block.lastUpdatedAt != null) ...<Widget>[
            Text(
              l10n.termsLastUpdated(_formatDate(block.lastUpdatedAt!)),
              style: const TextStyle(
                color: SimfTokens.txtTertiary,
                fontSize: SimfTokens.textSm,
              ),
            ),
            const SizedBox(height: SimfTokens.space3),
          ],
          SelectableText(
            body,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              fontSize: SimfTokens.textSm,
              height: 1.8,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildConsentGate(AppL10n l10n) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: const BoxDecoration(
        border: Border(top: BorderSide(color: SimfTokens.line2)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _buildCheckRow(l10n),
          const SizedBox(height: SimfTokens.space3),
          FilledButton(
            onPressed: _accepted ? _accept : null,
            child: Text(l10n.termsAcceptButton),
          ),
          TextButton(
            onPressed: _decline,
            child: Text(l10n.declineLabel),
          ),
        ],
      ),
    );
  }

  // .terms .check — bordered radius-6 row, accent box with navy tick + label.
  Widget _buildCheckRow(AppL10n l10n) {
    return InkWell(
      onTap: () => setState(() => _accepted = !_accepted),
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall + 2),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        decoration: BoxDecoration(
          color: SimfTokens.surfaceTint,
          border: Border.all(color: SimfTokens.line2),
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall + 2),
        ),
        child: Row(
          children: <Widget>[
            _buildCheckBox(),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: Text(
                l10n.termsAcceptCheckbox,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // .terms .box — 18px square, accent border + fill when ticked, navy ✓.
  Widget _buildCheckBox() {
    return Container(
      width: 18,
      height: 18,
      decoration: BoxDecoration(
        color: _accepted ? SimfTokens.accent : Colors.transparent,
        border: Border.all(color: SimfTokens.accent, width: 1.5),
        borderRadius: BorderRadius.circular(3),
      ),
      child: _accepted
          ? const Icon(
              Icons.check,
              size: 12,
              color: SimfTokens.navy,
            )
          : null,
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
