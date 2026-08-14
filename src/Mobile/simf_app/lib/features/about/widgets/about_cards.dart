import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/about/widgets/about_card.dart';
import 'package:simf_app/features/about/widgets/card_heading.dart';

/// A detail value's internal reading direction, from its script: an Arabic
/// value reads RTL; a language-neutral value (a year, a "01-2026 — 04-2026"
/// range, a URL) reads LTR so its segments keep their order. Block position is
/// pinned to the row's end separately, so this only fixes glyph/segment order.
TextDirection _valueDirection(String value) =>
    value.codeUnits.any((c) => c >= 0x0600 && c <= 0x06FF)
        ? TextDirection.rtl
        : TextDirection.ltr;

/// A titled navy-deep text card (الرسالة / الرؤية): the white heading over the
/// beige body paragraph.
class AboutTextCard extends StatelessWidget {
  const AboutTextCard({required this.title, required this.body, super.key});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return AboutCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          CardHeading(title),
          const SizedBox(height: SimfTokens.space2),
          Text(
            body,
            style: SimfTokens.bodyBeigeSm16,
          ),
        ],
      ),
    );
  }
}

/// The تفاصيل الملتقى card: the heading over "label : value" rows.
class AboutDetailsCard extends StatelessWidget {
  const AboutDetailsCard({required this.title, required this.rows, super.key});

  final String title;
  final List<(String, String)> rows;

  @override
  Widget build(BuildContext context) {
    return AboutCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          CardHeading(title),
          const SizedBox(height: SimfTokens.space3),
          for (final (index, (label, value)) in rows.indexed) ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space2),
            Row(
              children: <Widget>[
                Text(
                  '$label :',
                  style: SimfTokens.labelWhiteSemiboldSm,
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Align(
                    // Pin the value to the end edge (left in RTL), opposite the
                    // start-aligned label — the Figma details card's justified
                    // two-column row. Directional, so English mirrors it.
                    alignment: AlignmentDirectional.centerEnd,
                    child: Text(
                      value,
                      textDirection: _valueDirection(value),
                      style: SimfTokens.labelBeigeSm,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// The المحاور الرئيسية card: the heading over the numbered theme entries.
class AboutThemesCard extends StatelessWidget {
  const AboutThemesCard({required this.title, required this.themes, super.key});

  final String title;
  final List<(String, String, String)> themes;

  @override
  Widget build(BuildContext context) {
    return AboutCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          CardHeading(title),
          const SizedBox(height: SimfTokens.space3),
          for (final (index, (number, themeTitle, body)) in themes.indexed)
            ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space4),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  number,
                  style: SimfTokens.labelGoldBoldLg,
                ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        themeTitle,
                        style: SimfTokens.labelWhiteBoldMd,
                      ),
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        body,
                        style: SimfTokens.bodyBeigeSm15,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
