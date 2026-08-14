import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/sharing/content_sharer.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/data/share_qr_payload.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Share my contact (SIMF-FDS-014 §5.4–5.5, D-286). **Auth-gated** (Approved
/// only). On open it fetches the caller's dedicated share token
/// (`GET /app/account/share-token`, minted on first call) and renders it as a
/// QR another visitor can scan. The visitor can **rotate** the token (the old
/// code stops resolving) or hand off an OS share-intent **vCard** built from the
/// shipped My-Area export (`GET /app/account/contact-card.vcf`). The share token
/// is separate from the entry `QrId`, so scanning at the gate never harvests the
/// card. UI is interim (final visuals from SIMF-VID-001).
class ShareMyContactScreen extends ConsumerStatefulWidget {
  const ShareMyContactScreen({super.key});

  @override
  ConsumerState<ShareMyContactScreen> createState() =>
      _ShareMyContactScreenState();
}

class _ShareMyContactScreenState extends ConsumerState<ShareMyContactScreen> {
  bool _loading = true;
  bool _error = false;
  bool _rotating = false;
  bool _sharingVcard = false;
  String? _token;
  String? _vcard;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final token = await ref.read(contactsRepositoryProvider).getMyShareToken();
      // D-470 + D-737 — the QR encodes the user's vCard (Arabic name + phones)
      // with the share token embedded as an X-SIMF-TOKEN property: any phone
      // camera can add the contact, AND the in-app scanner can extract the token
      // to resolve + save the live card. Only the QR's content changed; the rest
      // of the screen (share .vcf, rotate) is unchanged.
      final vcard = await ref.read(myAreaRepositoryProvider).getContactCardVcf();
      if (!mounted) {
        return;
      }
      setState(() {
        _token = token;
        _vcard = vcard;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _token = null;
        _vcard = null;
        _loading = false;
      });
    }
  }

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
      final token = await ref.read(contactsRepositoryProvider).rotateShareToken();
      if (!mounted) {
        return;
      }
      setState(() {
        _token = token;
        _rotating = false;
      });
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
    // Anchor rect read before the await — the iPad share sheet must point at the
    // button as it was at tap time, and this element may be gone by then.
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
        onRefresh: _load,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    final vcard = _vcard;
    if (_error || vcard == null) {
      return SimfPullableHost(
        child: SimfErrorState(
          message: l10n.shareMyContactError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    return Center(
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              decoration: BoxDecoration(
                color: SimfTokens.surface,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                border: Border.all(color: SimfTokens.accent),
              ),
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: QrImageView(
                  data: buildShareQrPayload(vcard, _token ?? ''),
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
              onPressed: (_rotating || _token == null)
                  ? null
                  : () => unawaited(_rotate()),
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
                      child: CircularProgressIndicator(strokeWidth: SimfTokens.shareMyContactScreenStrokeWidth),
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
