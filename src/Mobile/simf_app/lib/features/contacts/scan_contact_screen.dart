import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/contact_models.dart';
import 'data/contacts_repository.dart';
import 'widgets/contact_card.dart';

/// Scan a visitor's QR → preview → save (SIMF-FDS-014 §5.5–5.6, D-286).
/// **Auth-gated** (Approved only). The camera (`mobile_scanner`) reads another
/// visitor's share QR; a **manual-entry** field is the fallback when the camera
/// is denied/unavailable and is the path the widget tests drive. Either way the
/// scanned code is resolved (`POST /app/contacts/resolve`) to a live card shown
/// in a preview sheet, where it can be saved to *My Contacts*
/// (`POST /app/contacts/save`, idempotent; saving yourself is a 400). UI is
/// interim (final visuals from SIMF-VID-001).
class ScanContactScreen extends ConsumerStatefulWidget {
  const ScanContactScreen({super.key, this.enableCamera = true});

  /// Off in widget tests (no camera in the test environment) so the manual-entry
  /// → resolve → preview → save path can be exercised without the native plugin.
  final bool enableCamera;

  @override
  ConsumerState<ScanContactScreen> createState() => _ScanContactScreenState();
}

class _ScanContactScreenState extends ConsumerState<ScanContactScreen> {
  final TextEditingController _manualController = TextEditingController();
  bool _processing = false;
  String? _lastHandled;

  @override
  void dispose() {
    _manualController.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_processing || capture.barcodes.isEmpty) {
      return;
    }
    final raw = capture.barcodes.first.rawValue?.trim() ?? '';
    if (raw.isEmpty || raw == _lastHandled) {
      return;
    }
    _lastHandled = raw;
    unawaited(_handleToken(raw));
  }

  Future<void> _handleToken(String token) async {
    final code = token.trim();
    if (code.isEmpty || _processing) {
      return;
    }
    final l10n = AppL10n.of(context);
    setState(() => _processing = true);
    try {
      final card = await ref.read(contactsRepositoryProvider).resolve(code);
      if (!mounted) {
        return;
      }
      setState(() => _processing = false);
      await _showPreview(code, card);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() => _processing = false);
      // Let the same code be retried after a failure (the debounce is reset).
      _lastHandled = null;
      ScaffoldMessenger.of(context).showSnackBar(
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
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => _ContactPreviewSheet(token: token, card: card),
    );
    if (saved == true && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).saveContactSaved)),
      );
      // Returning closes the scanner; the caller (My Contacts) reloads its list.
      unawaited(Navigator.of(context).maybePop());
    } else {
      // Sheet dismissed without saving — allow re-scanning the same code.
      _lastHandled = null;
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.scanContactTitle)),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            // The camera preview is confined to a fixed viewfinder box, never an
            // Expanded that fills the screen: some devices don't composite the
            // platform-view preview (it paints blank) and an unbounded preview
            // then covers the whole body — hiding the manual-entry fallback. A
            // bounded box keeps the manual entry below always visible + usable.
            SizedBox(
              width: double.infinity,
              height: 320,
              child: _buildCamera(l10n),
            ),
            Expanded(
              child: SingleChildScrollView(child: _buildManualEntry(l10n)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCamera(AppL10n l10n) {
    if (!widget.enableCamera) {
      return _CameraPlaceholder(label: l10n.scanContactCameraUnavailable);
    }
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        MobileScanner(
          onDetect: _onDetect,
          // A camera / ML-Kit / permission failure (e.g. a device with no
          // working Google Play Services) is surfaced here instead of crashing
          // the screen — the manual-entry field below stays the working path.
          errorBuilder: (context, error, child) =>
              _CameraPlaceholder(label: l10n.scanContactCameraUnavailable),
        ),
        if (_processing)
          const ColoredBox(
            color: Color(0x66000000),
            child: Center(child: CircularProgressIndicator()),
          ),
      ],
    );
  }

  Widget _buildManualEntry(AppL10n l10n) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.scanContactManualLabel,
            style: const TextStyle(
              color: SimfTokens.inkMuted,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Row(
            children: <Widget>[
              Expanded(
                child: TextField(
                  controller: _manualController,
                  decoration: InputDecoration(
                    labelText: l10n.scanContactManualField,
                    border: const OutlineInputBorder(),
                  ),
                  onSubmitted: (value) => unawaited(_handleToken(value)),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              FilledButton(
                onPressed: _processing
                    ? null
                    : () => unawaited(_handleToken(_manualController.text)),
                child: Text(l10n.scanContactResolve),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The static placeholder shown in place of the live camera (tests / camera
/// unavailable). The manual-entry row below stays the working path.
class _CameraPlaceholder extends StatelessWidget {
  const _CameraPlaceholder({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.field,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.qr_code_scanner,
              size: 56,
              color: SimfTokens.inkMuted,
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              label,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
          ],
        ),
      ),
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
