import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/sharing/content_sharer.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/data/share_qr_payload.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Share my contact (SIMF-FDS-014 §5.4–5.5, D-286). **Auth-gated** (Approved
/// only). On open it fetches the caller's dedicated share token (`GET
/// /app/account/share-token`, minted on first call) and renders it as a QR
/// another visitor can scan. The visitor can **rotate** the token (the old code
/// stops resolving) or hand off an OS share-intent **vCard** built from the
/// shipped My-Area export (`GET /app/account/contact-card.vcf`). The share
/// token is separate from the entry `QrId`, so scanning at the gate never
/// harvests the card. UI is interim (final visuals from SIMF-VID-001).
///
/// Route: `RouteNames.shareMyContact`.
/// Data: [contactsRepositoryProvider], [myAreaRepositoryProvider],
///       [shareCardProvider].
/// Perf: no list — a single-screen layout.
/// The share token plus the vCard body the QR encodes.
@immutable
class ShareCard {
  const ShareCard({required this.token, required this.vcard});

  final String token;
  final String vcard;

  ShareCard withToken(String next) => ShareCard(token: next, vcard: vcard);
}

/// An `AsyncNotifier`, because ROTATE replaces the token in place.
///
/// D-470 + D-737 — the QR encodes the user's vCard with the share token
/// embedded, and the payload is assembled from the two at render. So a rotate
/// only needs the new token: re-fetching the vCard body, which did not change,
/// would be a wasted round trip. That is what the old `setState(() => _token =
/// token)` did, and [rotate] is where it lives now.
class ShareCardNotifier extends AutoDisposeAsyncNotifier<ShareCard> {
  @override
  Future<ShareCard> build() async {
    final token = await ref.watch(contactsRepositoryProvider).getMyShareToken();
    final vcard = await ref.watch(myAreaRepositoryProvider).getContactCardVcf();
    return ShareCard(token: token, vcard: vcard);
  }

  Future<void> rotate() async {
    final token = await ref.read(contactsRepositoryProvider).rotateShareToken();
    final current = state.valueOrNull;
    if (current != null) {
      state = AsyncValue<ShareCard>.data(current.withToken(token));
    }
  }
}

final shareCardProvider =
    AsyncNotifierProvider.autoDispose<ShareCardNotifier, ShareCard>(
  ShareCardNotifier.new,
);

class ShareMyContactScreen extends ConsumerStatefulWidget {
  const ShareMyContactScreen({super.key});

  @override
  ConsumerState<ShareMyContactScreen> createState() =>
      _ShareMyContactScreenState();
}

class _ShareMyContactScreenState extends ConsumerState<ShareMyContactScreen> {
  bool _rotating = false;
  bool _sharingVcard = false;

  Future<void> _refresh() => refreshAsync(ref, shareCardProvider.future);

  Future<void> _rotate() async {
    final l10n = AppL10n.of(context);
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.shareMyContactRotateConfirmTitle,
      message: l10n.shareMyContactRotateConfirmBody,
      confirmLabel: l10n.shareMyContactRotate,
    );
    if (!confirmed || !mounted) {
      return;
    }
    setState(() => _rotating = true);
    try {
      await ref.read(shareCardProvider.notifier).rotate();
      if (!mounted) {
        return;
      }
      setState(() => _rotating = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.shareMyContactRotated)),
      );
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _rotating = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.shareMyContactError)),
      );
    }
  }

  /// Fetches the My-Area vCard and hands it to the OS share sheet (the same
  /// export the My-Area dashboard shares, so there is one vCard source).
  Future<void> _shareVcard() async {
    if (_sharingVcard) return;
    setState(() => _sharingVcard = true);
    final l10n = AppL10n.of(context);
    // Anchor rect read before the await — the iPad share sheet must point at
    // the button as it was at tap time, and this element may be gone by then.
    final origin = shareOriginFromContext(context);
    try {
      final vcf = await ref.read(myAreaRepositoryProvider).getContactCardVcf();
      await shareTextContent(
        content: vcf,
        filename: 'simf.vcf',
        mimeType: 'text/vcard',
        sharePositionOrigin: origin,
      );
    } on Object catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.shareFailed)),
      );
    } finally {
      if (mounted) setState(() => _sharingVcard = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.shareMyContactTitle,
      onBack: () => backOrHome(context),
      showSweep: true,
      body: SimfPullToRefresh(
        onRefresh: _refresh,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(shareCardProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfPullableHost(
            child: SimfErrorState(
              message: l10n.shareMyContactError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(shareCardProvider),
            ),
          ),
          data: (card) => _card(l10n, card),
        );
  }

  Widget _card(AppL10n l10n, ShareCard card) {
    return Center(
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            // NOT a DecoratedBox. Container insets its child by
            // BoxDecoration.padding, which is the border dimensions, and this
            // decoration has a border — the swap moved a golden by 2.42% when
            // it was tried (2026-08-14).
            // ignore: use_decorated_box
            Container(
              decoration: BoxDecoration(
                color: SimfTokens.surface,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                border: Border.all(color: SimfTokens.accent),
              ),
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: QrImageView(
                  data: buildShareQrPayload(card.vcard, card.token),
                  size: SimfTokens.shareMyContactScreenSize,
                ),
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            Text(
              l10n.shareMyContactHint,
              textAlign: TextAlign.center,
              style: SimfTokens.bodyWhite70,
            ),
            const SizedBox(height: SimfTokens.space5),
            FilledButton.icon(
              onPressed: () => unawaited(_shareVcard()),
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
                backgroundColor: SimfTokens.accent,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: const Icon(Icons.ios_share, color: SimfTokens.surface),
              label: Text(
                l10n.shareContact,
                style: SimfTokens.labelWhiteBoldLg,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            OutlinedButton.icon(
              onPressed: _rotating ? null : () => unawaited(_rotate()),
              style: OutlinedButton.styleFrom(
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
                side: const BorderSide(color: SimfTokens.accent),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: _rotating
                  ? const SizedBox(
                      width: SimfTokens.space4,
                      height: SimfTokens.space4,
                      child: CircularProgressIndicator(
                          strokeWidth:
                              SimfTokens.shareMyContactScreenStrokeWidth,),
                    )
                  : const Icon(Icons.autorenew, color: SimfTokens.accent),
              label: Text(
                l10n.shareMyContactRotate,
                style: SimfTokens.labelGoldBoldLg,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
