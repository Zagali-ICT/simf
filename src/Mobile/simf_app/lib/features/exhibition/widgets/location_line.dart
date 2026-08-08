import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/country_flag.dart';

/// The gold "City، Country" line with the country flag (Figma 1439:11895):
/// SemiBold-14 gold city, 20px flag, 8px gap, flag on the left (RTL).
class LocationLine extends StatelessWidget {
  const LocationLine({required this.text, required this.countryId});

  final String text;
  final int? countryId;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(countryId);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            text,
            textAlign: TextAlign.center,
            style: SimfTokens.labelGoldSemibold, // 14
          ),
        ),
        if (flag != null) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          Text(
            flag,
            textDirection: TextDirection.ltr,
            style: const TextStyle(fontSize: SimfTokens.textXl, height: 1), // 20
          ),
        ],
      ],
    );
  }
}
