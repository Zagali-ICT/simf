import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../core/sharing/content_sharer.dart';
import '../myarea/data/myarea_repository.dart';
import 'data/contacts_repository.dart';

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
      // D-470 — the QR now encodes the user's vCard (Arabic name + phones) so any
      // phone camera can add the contact. Only the QR's content changed; the rest
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
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.shareMyContactRotateConfirmTitle),
        content: Text(l10n.shareMyContactRotateConfirmBody),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.cancelLabel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.shareMyContactRotate),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
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
    final l10n = AppL10n.of(context);
    try {
      final vcf = await ref.read(myAreaRepositoryProvider).getContactCardVcf();
      await shareTextContent(
        content: vcf,
        filename: 'simf.vcf',
        mimeType: 'text/vcard',
      );
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.shareFailed)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.shareMyContactTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    final vcard = _vcard;
    if (_error || vcard == null) {
      return _ErrorState(
        message: l10n.shareMyContactError,
        onRetry: () => unawaited(_load()),
      );
    }
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Card(
              margin: EdgeInsets.zero,
              color: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
              ),
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: QrImageView(
                  data: vcard,
                  version: QrVersions.auto,
                  size: 240,
                  gapless: true,
                ),
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            Text(
              l10n.shareMyContactHint,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
            const SizedBox(height: SimfTokens.space5),
            FilledButton.icon(
              onPressed: () => unawaited(_shareVcard()),
              icon: const Icon(Icons.ios_share),
              label: Text(l10n.shareContact),
            ),
            const SizedBox(height: SimfTokens.space2),
            TextButton.icon(
              onPressed: (_rotating || _token == null)
                  ? null
                  : () => unawaited(_rotate()),
              icon: _rotating
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.autorenew),
              label: Text(l10n.shareMyContactRotate),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
