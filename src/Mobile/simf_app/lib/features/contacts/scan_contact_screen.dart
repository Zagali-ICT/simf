import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/scan_start_prompt.dart';
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
  // The live camera starts only when the user taps "scan" — while it is off the
  // on-screen back / cancel / manual entry stay tappable on devices where the
  // live camera grabs taps window-wide (Huawei/EMUI). (D-426)
  bool _cameraOn = false;
  String? _lastHandled;

  /// Explicit controller with **no-duplicate** detection so the camera reports a
  /// code once and never re-fires it until a *different* code appears — the
  /// camera-level guard against the re-scan loop (D-426). Created only when the
  /// live camera is used (off in widget tests / web).
  MobileScannerController? _scannerController;

  @override
  void initState() {
    super.initState();
    if (widget.enableCamera) {
      _scannerController = MobileScannerController(
        detectionSpeed: DetectionSpeed.noDuplicates,
      );
    }
  }

  @override
  void dispose() {
    _manualController.dispose();
    unawaited(_scannerController?.dispose());
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
      // Keep _processing true through the preview so the camera doesn't re-fire
      // while the sheet is open.
      await _showPreview(code, card);
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      // Do NOT reset _lastHandled here: the QR is still in front of the camera,
      // and re-arming it re-resolved the same (often un-resolvable) code on the
      // next frame — an endless error loop (D-426). The code is handled once;
      // use the manual-entry field to look a code up again.
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            e.httpStatus == 404
                ? l10n.scanContactNotFound
                : l10n.scanContactError,
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _processing = false);
      }
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
      // Returning closes the scanner; the caller reloads its list.
      _leave();
    }
    // On dismiss without saving we intentionally keep the code debounced
    // (_lastHandled stays set) so the scanner does NOT immediately re-open the
    // preview for the same QR still in view — that was the scan loop (D-426).
    // The manual-entry field looks a code up again on demand.
  }

  /// Leaves the scanner reliably. The screen can be reached as the root of its
  /// navigator (deep-link / route-restore / a push from the shell that didn't
  /// stack), so the local navigator may not be able to pop — in which case the
  /// default AppBar back is absent and a system back would exit the app. Pop the
  /// route when possible, otherwise go back to the badge tab. Uses the Navigator
  /// for the pop (works without a GoRouter, e.g. in widget tests) and
  /// `GoRouter.maybeOf` for the fallback so it never throws when go_router is
  /// absent. (D-423 follow-up: the scanner had no working back.)
  void _leave() {
    final navigator = Navigator.of(context);
    if (navigator.canPop()) {
      navigator.pop();
    } else {
      GoRouter.maybeOf(context)?.goNamed(RouteNames.badge);
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // When the scanner is the navigator root, the system back would exit the
    // app; intercept it and route to the badge instead. When it was pushed,
    // canPop is true and normal pops (incl. the save-flow close) pass through.
    final canPop = Navigator.of(context).canPop();
    return PopScope(
      canPop: canPop,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          GoRouter.maybeOf(context)?.goNamed(RouteNames.badge);
        }
      },
      child: Scaffold(
      appBar: AppBar(
        title: Text(l10n.scanContactTitle),
        // On-screen back for normal devices; the AppBar is above the camera box.
        // While the live camera is active some devices (Huawei/EMUI) swallow all
        // on-screen taps window-wide — there the system back (PopScope above) is
        // the way out. The scanner is never auto-resumed into (route_resume). The
        // scanner is reached intentionally, so canPop is normally true. (D-426)
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: MaterialLocalizations.of(context).backButtonTooltip,
          onPressed: _leave,
        ),
      ),
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            // The live viewfinder takes the bulk of the page height (responsive
            // to rotation) with the manual-entry fallback in a compact strip
            // below it — both reachable, no scrolling needed. The box is sized
            // explicitly (a large fraction, NOT a full-height Expanded): some
            // devices only composite the camera platform-view when it is
            // bounded — a full-bleed preview paints blank and covers the body —
            // and a bounded box also keeps the manual entry from being hidden.
            // When the camera is off (test / web) the box is compact.
            final cameraHeight = widget.enableCamera
                ? (constraints.maxHeight * 0.78).clamp(240.0, constraints.maxHeight)
                : 220.0;
            return Column(
              children: <Widget>[
                SizedBox(
                  width: double.infinity,
                  height: cameraHeight,
                  child: _buildCamera(l10n),
                ),
                Expanded(
                  child: SingleChildScrollView(child: _buildManualEntry(l10n)),
                ),
              ],
            );
          },
        ),
      ),
      ),
    );
  }

  Widget _buildCamera(AppL10n l10n) {
    if (!widget.enableCamera) {
      return _CameraPlaceholder(label: l10n.scanContactCameraUnavailable);
    }
    if (!_cameraOn) {
      // Camera off by default — a tappable prompt starts it. Keeps the back /
      // manual entry usable before the live camera (which can swallow taps).
      return ScanStartPrompt(
        label: l10n.scanStartCamera,
        onStart: () => setState(() => _cameraOn = true),
      );
    }
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        MobileScanner(
          controller: _scannerController,
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
