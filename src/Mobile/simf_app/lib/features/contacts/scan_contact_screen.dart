import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/widgets/qr_scan_view.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../core/sharing/content_sharer.dart';
import 'data/contact_models.dart';
import 'data/contacts_repository.dart';
import 'data/share_qr_payload.dart';
import 'widgets/contact_preview_sheet.dart';

/// Scan a visitor's QR → preview → save (SIMF-FDS-014 §5.5–5.6, D-286).
/// **Auth-gated** (Approved only). Uses the shared [QrScanView] (D-430): the
/// manual-entry path always works and the bounded opt-in camera can never trap
/// the user on EMUI (D-426). A scanned/typed code is resolved
/// (`POST /app/contacts/resolve`) to a live card shown in a preview sheet, where
/// it can be saved to *My Contacts* (`POST /app/contacts/save`, idempotent;
/// saving yourself is a 400). A scanned QR that is a **plain vCard with no SIMF
/// share token** (a foreign phone's contact, an old QR) can't resolve to a live
/// card, so it is offered straight to the phone's address book instead (D-744).
class ScanContactScreen extends ConsumerStatefulWidget {
  const ScanContactScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera) so the manual-entry → resolve → preview →
  /// save path can be exercised without the native plugin.
  final bool enableCamera;

  @override
  ConsumerState<ScanContactScreen> createState() => _ScanContactScreenState();
}

class _ScanContactScreenState extends ConsumerState<ScanContactScreen> {
  void _leave() {
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop();
    }
  }

  Future<void> _handleToken(String scanned) async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    // The app's own share QR is a vCard with the token embedded (D-737); a
    // native phone's vCard, or an old QR, carries no token → clear guidance.
    String code = scanned.trim();
    if (isVCardPayload(code)) {
      final embedded = extractShareToken(code);
      if (embedded == null || embedded.isEmpty) {
        // Not a SIMF member (a foreign phone's vCard, or an old QR): it can't
        // resolve to a live in-app card, but it IS a full vCard — offer it to
        // the phone's own address book instead of dead-ending (D-744).
        await _saveVcardToPhone(code);
        return;
      }
      code = embedded;
    }
    if (code.isEmpty) {
      return;
    }
    try {
      final card = await ref.read(contactsRepositoryProvider).resolve(code);
      if (!mounted) {
        return;
      }
      await _showPreview(code, card);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            e.httpStatus == 404
                ? l10n.scanContactNotFound
                : l10n.scanContactError,
          ),
        ),
      );
    }
  }

  /// Hands a non-SIMF vCard to the OS "add contact" flow (D-744). Reuses the
  /// same share path as *Share my contact*, so the phone's Contacts app imports
  /// the card and the user confirms the save — no WRITE_CONTACTS permission.
  Future<void> _saveVcardToPhone(String vcard) async {
    final l10n = AppL10n.of(context);
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.scanContactSaveToPhoneTitle,
      message: l10n.scanContactSaveToPhoneBody,
      confirmLabel: l10n.scanContactSaveToPhoneConfirm,
    );
    if (!confirmed || !mounted) {
      return;
    }
    try {
      await shareTextContent(
        content: vcard,
        filename: 'contact.vcf',
        mimeType: 'text/vcard',
        sharePositionOrigin: shareOriginFromContext(context),
      );
    } on Exception {
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.scanContactSaveToPhoneFailed)),
      );
    }
  }

  Future<void> _showPreview(String token, VisitorCard card) async {
    final messenger = ScaffoldMessenger.of(context);
    final savedLabel = AppL10n.of(context).saveContactSaved;
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => ContactPreviewSheet(token: token, card: card),
    );
    if (saved == true && mounted) {
      messenger.showSnackBar(SnackBar(content: Text(savedLabel)));
      // Returning closes the scanner; the caller reloads its list.
      _leave();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return QrScanView(
      title: l10n.scanContactTitle,
      fieldLabel: l10n.scanContactManualField,
      continueLabel: l10n.scanContactResolve,
      enableCamera: widget.enableCamera,
      onCode: _handleToken,
      onLeave: _leave,
    );
  }
}
