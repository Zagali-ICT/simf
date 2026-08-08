import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/motion/motion_durations.dart';
import '../../core/responsive/breakpoints.dart';
import '../theme/tokens.dart';
import 'scan_line.dart';

/// The gold border weight of the viewfinder's corner brackets (Figma 758:4579).
const BorderSide _bracketSide = BorderSide(color: SimfTokens.accent, width: 2.36);

/// Card widths per window-size class (BUG-019 / 19e). Figma draws the phone card
/// at 343 (758:4735); pinning that on a tablet left the gate operator squinting
/// at a phone-sized viewfinder on a 1600px panel, so the card steps up and
/// clamps instead of stretching edge-to-edge.
const double _cardWidthCompact = 343;
const double _cardWidthMedium = 480;
const double _cardWidthExpanded = 560;

/// The camera window's height as a fraction of the card width, so the Figma
/// 343 × 300 proportion holds at every size.
const double _windowHeightRatio = 300 / _cardWidthCompact;

/// Where the resting (not scanning) scan line sits inside the window — Figma's
/// 93px on the 300px window, kept proportional.
const double _scanLineRestRatio = 93 / 300;

/// Bracket-clearance insets for the animated scan-line sweep.
const double _sweepTopInset = 28;
const double _sweepBottomInset = 30;

/// The QR-scanner viewfinder card from Figma node 758:4735 — a navy card holding
/// a black camera window (gold corner brackets, a glowing gold scan line and a
/// centred scan glyph) above a "searching" caption + track.
///
/// The live camera is injected as [camera] (e.g. a `flutter_zxing` reader) so
/// this widget stays plugin-free and rendable in tests; when it is null the
/// window paints the brackets + glyph on black (the camera-off / preview state).
///
/// D-739 (owner): the gold scan line **sweeps up and down** across the window
/// while [active] (a live camera is scanning) — the real "scanning" motion — and
/// rests at the design position otherwise. This is the single shared viewfinder
/// used by every scanner in the app, so the look + motion are identical
/// everywhere.
class SimfScannerFrame extends StatefulWidget {
  const SimfScannerFrame({
    required this.statusLabel,
    this.camera,
    this.active = false,
    super.key,
  });

  /// The live camera preview painted behind the overlay; null = camera-off.
  final Widget? camera;

  /// The "actively searching" caption.
  final String statusLabel;

  /// Animate the scan line's up/down sweep (a live camera is scanning). False =
  /// the line rests at its design position.
  final bool active;

  @override
  State<SimfScannerFrame> createState() => _SimfScannerFrameState();
}

class _SimfScannerFrameState extends State<SimfScannerFrame>
    with SingleTickerProviderStateMixin {
  // Built eagerly in initState, NOT as a lazy `late final` initialiser: a frame
  // that never animates (mounted with `active: false`) would otherwise create
  // its ticker for the first time inside dispose(), which throws "looking up a
  // deactivated widget's ancestor is unsafe". Surfaced by the 19e width test.
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: MotionDurations.scannerSweep,
    );
    if (widget.active) {
      _controller.repeat(reverse: true);
    }
  }

  @override
  void didUpdateWidget(SimfScannerFrame oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !_controller.isAnimating) {
      _controller.repeat(reverse: true);
    } else if (!widget.active && _controller.isAnimating) {
      _controller.stop();
      _controller.value = 0;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// The card width for the current window-size class, never wider than the
  /// screen itself (BUG-019 / 19e).
  static double _cardWidth(BuildContext context) {
    final double preferred;
    switch (WindowSize.of(context)) {
      case WindowSize.compact:
        preferred = _cardWidthCompact;
      case WindowSize.medium:
        preferred = _cardWidthMedium;
      case WindowSize.expanded:
      case WindowSize.large:
        preferred = _cardWidthExpanded;
    }
    return math.min(preferred, MediaQuery.sizeOf(context).width);
  }

  @override
  Widget build(BuildContext context) {
    final width = _cardWidth(context);
    final windowHeight = width * _windowHeightRatio;
    return Container(
      width: width,
      padding: const EdgeInsets.symmetric(vertical: 16),
      decoration: BoxDecoration(
        color: SimfTokens.scannerCard,
        borderRadius: BorderRadius.circular(24),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: SimfTokens.scrimBlack25,
            blurRadius: SimfTokens.simfScannerFrameBlurRadius,
            offset: Offset(0, 24),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _buildWindow(windowHeight),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 20, 16, 4),
            child: _buildStatusRow(),
          ),
        ],
      ),
    );
  }

  Widget _buildWindow(double windowHeight) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        height: windowHeight,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            ColoredBox(color: SimfTokens.black, child: widget.camera),
            const ColoredBox(color: SimfTokens.scrimBlack35), // black @ 35%
            const Positioned(top: 16, left: 16, child: _Bracket(top: true, left: true)),
            const Positioned(top: 16, right: 16, child: _Bracket(top: true, left: false)),
            const Positioned(bottom: 16, left: 16, child: _Bracket(top: false, left: true)),
            const Positioned(bottom: 16, right: 16, child: _Bracket(top: false, left: false)),
            const Center(
              child: Icon(
                Icons.qr_code_scanner,
                color: SimfTokens.accent,
                size: SimfTokens.simfScannerFrameSize,
              ),
            ),
            _buildScanLine(windowHeight),
          ],
        ),
      ),
    );
  }

  /// The glowing gold scan line — swept vertically across the window by the
  /// controller when [active], otherwise pinned at the design position.
  Widget _buildScanLine(double windowHeight) {
    // Sweep between a top and bottom margin, keeping the line clear of the
    // corner brackets.
    const topEdge = _sweepTopInset;
    final bottomEdge = windowHeight - _sweepBottomInset;
    if (!widget.active) {
      return Positioned(
        top: windowHeight * _scanLineRestRatio,
        left: 16,
        right: 16,
        child: const ScanLine(),
      );
    }
    return AnimatedBuilder(
      animation: _controller,
      child: const ScanLine(),
      builder: (context, child) => Positioned(
        top: topEdge + (bottomEdge - topEdge) * _controller.value,
        left: 16,
        right: 16,
        child: child!,
      ),
    );
  }

  /// The "جارٍ فحص الرمز…" caption over a thin gold track (Figma 758:4596/4598).
  Widget _buildStatusRow() {
    return Directionality(
      // Forced LTR so the caption sits at the end (right) as in the frame.
      textDirection: TextDirection.ltr,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Align(
            alignment: Alignment.centerRight,
            child: Text(
              widget.statusLabel,
              textDirection: TextDirection.rtl,
              style: const TextStyle(color: SimfTokens.mutedBlue, fontSize: SimfTokens.simfScannerFrameFontSize),
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          ClipRRect(
            borderRadius: BorderRadius.circular(100),
            child: const SizedBox(
              height: SimfTokens.simfScannerFrameHeightSm,
              child: ColoredBox(color: SimfTokens.scannerTrack),
            ),
          ),
        ],
      ),
    );
  }
}

/// One gold L-shaped corner bracket of the viewfinder (Figma 758:4579-4582):
/// a 28px square drawing the [top]-or-bottom and [left]-or-right edges in gold.
class _Bracket extends StatelessWidget {
  const _Bracket({required this.top, required this.left});

  final bool top;
  final bool left;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.simfScannerFrameWidth,
      height: SimfTokens.simfScannerFrameHeightLg,
      decoration: BoxDecoration(
        border: Border(
          top: top ? _bracketSide : BorderSide.none,
          bottom: top ? BorderSide.none : _bracketSide,
          left: left ? _bracketSide : BorderSide.none,
          right: left ? BorderSide.none : _bracketSide,
        ),
      ),
    );
  }
}
