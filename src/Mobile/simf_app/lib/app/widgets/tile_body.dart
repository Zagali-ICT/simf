import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The shared tile interior: a centred top element over the small bold label.
class TileBody extends StatelessWidget {
  const TileBody({required this.top, required this.label, required this.labelColor, super.key,
    this.minHeight = 72,
  });

  final Widget top;
  final String label;
  final Color labelColor;
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(minHeight: minHeight),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            top,
            const SizedBox(height: SimfTokens.space2),
            Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
                color: labelColor,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
