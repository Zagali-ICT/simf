import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class LegendItem extends StatelessWidget {
  const LegendItem({
    required this.color,
    required this.label,
    required this.size,
    super.key,
    this.borderColor,
    this.icon,
    this.iconColor,
  });

  final Color color;
  final String label;
  final double size;
  final Color? borderColor;
  final IconData? icon;
  final Color? iconColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          label,
          style: SimfTokens.labelBeigeSm,
        ),
        const SizedBox(width: SimfTokens.space2),
        Container(
          width: size,
          height: size,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: color,
            border:
                borderColor != null ? Border.all(color: borderColor!) : null,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSeat),
          ),
          child: icon != null
              ? Icon(
                  icon,
                  size: SimfTokens.seatStateIconSize,
                  color: iconColor,
                )
              : null,
        ),
      ],
    );
  }
}
