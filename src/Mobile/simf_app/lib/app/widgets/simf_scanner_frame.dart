import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The gold border weight of the viewfinder's corner brackets (Figma 758:4579).
const BorderSide _bracketSide = BorderSide(color: SimfTokens.accent, width: 2.36);

/// The QR-scanner viewfinder card from Figma node 758:4735 — a navy card holding
/// a black camera window (gold corner brackets, a gold scan line and a centred
/// scan glyph) above a "searching" caption + scan bar.
///
/// The live camera is injected as [camera] (e.g. a `flutter_zxing` reader) so
/// this widget stays plugin-free and rendable in tests; when it is null the
/// window paints the brackets + glyph on black (the camera-off / preview state).
///
/// Deliberate Figma deviation (D-737): 758:4735 shows the scan bar as a static
/// "30%" snapshot. A progress bar that never advances reads as broken, so the
/// bar is an **indeterminate looping gold sweep** while [active] (a live camera
/// is scanning) and an empty track otherwise. The fake percentage label is gone.
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

  /// Animate the scan sweep (a live camera is scanning). False = static track.
  final bool active;

  @override
  State<SimfScannerFrame> createState() => _SimfScannerFrameState();
}

class _SimfScannerFrameState extends State<SimfScannerFrame>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1600),
  );

  @override
  void initState() {
    super.initState();
    if (widget.active) {
      _controller.repeat();
    }
  }

  @override
  void didUpdateWidget(SimfScannerFrame oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !_controller.isAnimating) {
      _controller.repeat();
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
            child: _buildScanBar(),
          ),
        ],
      ),
    );
  }

  Widget _buildWindow() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        height: 300,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            ColoredBox(color: Colors.black, child: widget.camera),
            const ColoredBox(color: Color(0x59000000)), // black @ 35% overlay
            const Positioned(top: 16, left: 16, child: _Bracket(top: true, left: true)),
            const Positioned(top: 16, right: 16, child: _Bracket(top: true, left: false)),
            const Positioned(bottom: 16, left: 16, child: _Bracket(top: false, left: true)),
            const Positioned(bottom: 16, right: 16, child: _Bracket(top: false, left: false)),
            Positioned(
              top: 93,
              left: 16,
              right: 16,
              child: Container(
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
              ),
            ),
            const Center(
              child: Icon(
                Icons.qr_code_scanner,
                color: SimfTokens.accent,
                size: 64,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildScanBar() {
    // Forced LTR so the caption sits at the end (right) as in the frame.
    return Directionality(
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
            child: SizedBox(
              height: 6,
              child: ColoredBox(
                color: SimfTokens.scannerTrack,
                child: widget.active
                    ? AnimatedBuilder(
                        animation: _controller,
                        // The gold segment is static — only its alignment
                        // animates, so build it once and slide it each frame.
                        child: const FractionallySizedBox(
                          widthFactor: 0.35,
                          child: DecoratedBox(
                            decoration: BoxDecoration(
                              gradient: LinearGradient(
                                colors: <Color>[
                                  Color(0x00C9A84C),
                                  SimfTokens.accent,
                                  Color(0xFFE8C96E),
                                  Color(0x00C9A84C),
                                ],
                              ),
                            ),
                          ),
                        ),
                        builder: (context, child) => Align(
                          alignment: Alignment(_controller.value * 2 - 1, 0),
                          child: child,
                        ),
                      )
                    : const SizedBox.shrink(),
              ),
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
