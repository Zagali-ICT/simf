import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/sharing/content_sharer.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/contacts/data/contacts_providers.dart';
import 'package:simf_app/features/contacts/data/share_qr_payload.dart';
import 'package:simf_app/features/contacts/widgets/share_contact_card.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Share my contact — route: RouteNames.shareMyContact
/// Contract: SIMF-FDS-014 §5.4–5.5, D-286 — the share token (`GET
///   /app/account/share-token`, rotatable) is separate from the entry `QrId`,
///   so scanning at the gate never harvests the card. Final visuals from
///   SIMF-VID-001.
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
        child: ref.watch(shareCardProvider).when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) => SimfPullableHost(
                child: SimfErrorState(
                  message: l10n.shareMyContactError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(shareCardProvider),
                ),
              ),
              data: (card) => ShareContactCard(
                qrData: buildShareQrPayload(card.vcard, card.token),
                rotating: _rotating,
                onShare: () => unawaited(_shareVcard()),
                onRotate: () => unawaited(_rotate()),
              ),
            ),
      ),
    );
  }
}
