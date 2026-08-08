import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class CardHeading extends StatelessWidget {
  const CardHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: SimfTokens.labelWhiteBoldMd,
    );
  }
}
