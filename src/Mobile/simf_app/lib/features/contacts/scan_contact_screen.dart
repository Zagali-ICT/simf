import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/qr_scan_view.dart';
import 'data/contact_models.dart';
import 'data/contacts_repository.dart';
import 'data/share_qr_payload.dart';
import 'widgets/contact_card.dart';

/// Scan a visitor's QR → preview → save (SIMF-FDS-014 §5.5–5.6, D-286).
/// **Auth-gated** (Approved only). Uses the shared [QrScanView] (D-430): the
/// manual-entry path always works and the bounded opt-in camera can never trap
/// the user on EMUI (D-426). A scanned/typed code is resolved
/// (`POST /app/contacts/resolve`) to a live card shown in a preview sheet, where
/// it can be saved to *My Contacts* (`POST /app/contacts/save`, idempotent;
/// saving yourself is a 400).
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
        messenger.showSnackBar(
          SnackBar(content: Text(l10n.scanContactVcardNoToken)),
        );
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

  Future<void> _showPreview(String token, VisitorCard card) async {
    final messenger = ScaffoldMessenger.of(context);
    final savedLabel = AppL10n.of(context).saveContactSaved;
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => _ContactPreviewSheet(token: token, card: card),
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

/// The resolved-card preview + save sheet. Holds the optional note and the save
/// call; pops `true` on a successful save, surfacing the self-save 400 inline.
class _ContactPreviewSheet extends ConsumerStatefulWidget {
  const _ContactPreviewSheet({required this.token, required this.card});

  final String token;
  final VisitorCard card;

  @override
  ConsumerState<_ContactPreviewSheet> createState() =>
      _ContactPreviewSheetState();
}

class _ContactPreviewSheetState extends ConsumerState<_ContactPreviewSheet> {
  final TextEditingController _noteController = TextEditingController();
  bool _saving = false;

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final l10n = AppL10n.of(context);
    setState(() => _saving = true);
    try {
      await ref
          .read(contactsRepositoryProvider)
          .save(widget.token, _noteController.text);
      if (!mounted) {
        return;
      }
      Navigator.of(context).pop(true);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            e.httpStatus == 400
                ? l10n.saveContactSelf
                : l10n.saveContactError,
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final card = widget.card;
    return Padding(
      padding: EdgeInsets.only(
        left: SimfTokens.space4,
        right: SimfTokens.space4,
        top: SimfTokens.space4,
        bottom: MediaQuery.of(context).viewInsets.bottom + SimfTokens.space4,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.contactPreviewTitle,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textLg,
              ),
            ),
            const SizedBox(height: SimfTokens.space3),
            ContactCard(
              name: card.localizedName(isArabic),
              available: card.available,
              jobTitle: card.jobTitle,
              organisation: card.localizedOrganisation(isArabic),
              country: card.localizedCountry(isArabic),
              email: card.email,
              saudiMobile: card.saudiMobile,
              internationalMobile: card.internationalMobile,
            ),
            if (card.available) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              TextField(
                controller: _noteController,
                decoration: InputDecoration(
                  labelText: l10n.saveContactNoteHint,
                  border: const OutlineInputBorder(),
                ),
                maxLength: 280,
              ),
              const SizedBox(height: SimfTokens.space2),
              FilledButton.icon(
                onPressed: _saving ? null : () => unawaited(_save()),
                icon: _saving
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.person_add_alt_1),
                label: Text(l10n.saveContactLabel),
              ),
            ],
            const SizedBox(height: SimfTokens.space2),
          ],
        ),
      ),
    );
  }
}
