import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The gold border weight of the viewfinder's corner brackets (Figma 758:4579).
const BorderSide _bracketSide = BorderSide(color: SimfTokens.accent, width: 2.36);

/// Height of the black camera window (Figma 758:4735).
const double _windowHeight = 300;

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
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2200),
  );

  @override
  void initState() {
    super.initState();
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

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 343,
      padding: const EdgeInsets.symmetric(vertical: 16),
      decoration: BoxDecoration(
        color: SimfTokens.scannerCard,
        borderRadius: BorderRadius.circular(24),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x40000000),
            blurRadius: 60,
            offset: Offset(0, 24),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _buildWindow(),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 20, 16, 4),
            child: _buildStatusRow(),
          ),
        ],
      ),
    );
  }

  Widget _buildWindow() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        height: _windowHeight,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            ColoredBox(color: Colors.black, child: widget.camera),
            const ColoredBox(color: Color(0x59000000)), // black @ 35% overlay
            const Positioned(top: 16, left: 16, child: _Bracket(top: true, left: true)),
            const Positioned(top: 16, right: 16, child: _Bracket(top: true, left: false)),
            const Positioned(bottom: 16, left: 16, child: _Bracket(top: false, left: true)),
            const Positioned(bottom: 16, right: 16, child: _Bracket(top: false, left: false)),
            const Center(
              child: Icon(
                Icons.qr_code_scanner,
                color: SimfTokens.accent,
                size: 64,
              ),
            ),
            _buildScanLine(),
          ],
        ),
      ),
    );
  }

  /// The glowing gold scan line — swept vertically across the window by the
  /// controller when [active], otherwise pinned at the design position.
  Widget _buildScanLine() {
    // Sweep between a top and bottom margin, keeping the line clear of the
    // corner brackets.
    const double topEdge = 28;
    const double bottomEdge = _windowHeight - 30;
    if (!widget.active) {
      return const Positioned(top: 93, left: 16, right: 16, child: _ScanLine());
    }
    return AnimatedBuilder(
      animation: _controller,
      child: const _ScanLine(),
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
              style: const TextStyle(color: SimfTokens.mutedBlue, fontSize: 12),
            ),
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(100),
            child: const SizedBox(
              height: 6,
              child: ColoredBox(color: SimfTokens.scannerTrack),
            ),
          ),
        ],
      ),
    );
  }
}

/// The horizontal glowing gold scan line (Figma 758:4735).
class _ScanLine extends StatelessWidget {
  const _ScanLine();

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 2,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: <Color>[
            Color(0x00C9A84C),
            SimfTokens.accent,
            Color(0x00C9A84C),
          ],
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(color: SimfTokens.accent, blurRadius: 8),
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
      width: 28,
      height: 28,
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
