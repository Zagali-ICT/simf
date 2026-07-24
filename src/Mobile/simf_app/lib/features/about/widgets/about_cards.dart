import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The detail value's script decides its direction: a value containing any
/// Arabic-block character (U+0600..U+06FF) reads RTL, a language-neutral value
/// (a year, a date range, a URL, an email) reads LTR so its digits/segments
/// keep their natural order.
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
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
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
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
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
                  child: Text(
                    value,
                    textDirection: _valueDirection(value),
                    textAlign: TextAlign.justify,
                    style: SimfTokens.labelBeigeSm,
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
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
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

/// The shared navy-deep card chrome for the About sections.
class _Card extends StatelessWidget {
  const _Card({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}

class _CardHeading extends StatelessWidget {
  const _CardHeading(this.text);

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
