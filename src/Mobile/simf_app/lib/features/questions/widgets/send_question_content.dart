import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The frame 943:3750 footnote — a single centred gold bullet, the bold gold
/// "ملاحظة" word, then the muted-beige "reviewed before air" body.
class ReviewNote extends StatelessWidget {
  const ReviewNote({required this.label, required this.body, super.key});

  final String label;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Text.rich(
      TextSpan(
        children: <InlineSpan>[
          const TextSpan(
            text: '• ',
            style: TextStyle(color: SimfTokens.accent),
          ),
          TextSpan(
            text: '$label ',
            style: const TextStyle(
              color: SimfTokens.accent,
              fontWeight: FontWeight.w600,
              fontSize: SimfTokens.textLg,
            ),
          ),
          TextSpan(
            text: body,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd,
            ),
          ),
        ],
      ),
      textAlign: TextAlign.center,
    );
  }
}

/// The "الاسئلة" section label (frame 945:3756) over the fixed 100px tinted
/// question box (frame 934:3668): navyDeep fill on the 8px radius (no border),
/// the placeholder pinned top + beige + inline-end aligned, max 500 chars.
class SendQuestionComposer extends StatelessWidget {
  const SendQuestionComposer({
    required this.sectionLabel,
    required this.hint,
    required this.controller,
    required this.errorText,
    required this.onChanged,
    super.key,
  });

  final String sectionLabel;
  final String hint;
  final TextEditingController controller;
  final String? errorText;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Frame 945:3756 — white, Medium, aligned to the inline end (right in RTL).
        Text(
          sectionLabel,
          // TextAlign.start = right under RTL (TextAlign.end would be left).
          textAlign: TextAlign.start,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w500,
            fontSize: SimfTokens.textLg,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Container(
          height: 100,
          decoration: BoxDecoration(
            color: SimfTokens.navyDeep,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space3,
          ),
          child: TextField(
            controller: controller,
            maxLength: 500,
            maxLines: null,
            expands: true,
            textAlign: TextAlign.start,
            textAlignVertical: TextAlignVertical.top,
            textInputAction: TextInputAction.newline,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
            ),
            cursorColor: SimfTokens.accent,
            decoration: InputDecoration(
              isCollapsed: true,
              border: InputBorder.none,
              counterText: '',
              hintText: hint,
              hintStyle: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
              ),
              errorText: errorText,
              errorStyle: const TextStyle(color: SimfTokens.danger),
            ),
            onChanged: onChanged,
          ),
        ),
      ],
    );
  }
}

/// The frame 942:3746 gold full-width submit: white SemiBold label on the
/// 4px-radius accent fill. The size/weight ride the label [Text] (not
/// `styleFrom.textStyle`) so the Arabic label keeps the theme's brand font — an
/// inline `styleFrom.textStyle` drops fontFamily and tofus the Arabic
/// (D-546/D-549; the frozen golden had locked that tofu).
class SendQuestionSubmitButton extends StatelessWidget {
  const SendQuestionSubmitButton({
    required this.label,
    required this.onPressed,
    super.key,
  });

  final String label;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: 44,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
        child: Text(
          label,
          style: const TextStyle(
            fontSize: SimfTokens.textSm,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

/// The frame 1049:12590 "بيانات الجلسة" block: the white Medium section header
/// over the session-data lines rendered as a right-aligned numbered list
/// (frame 1049:12591-12594), each line `#C2B8A2` 14px Medium.
class SessionDataBlock extends StatelessWidget {
  const SessionDataBlock({required this.label, required this.lines, super.key});

  final String label;
  final List<String> lines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Text(
          label,
          // TextAlign.start = right under RTL (TextAlign.end would be left).
          textAlign: TextAlign.start,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w500,
            fontSize: SimfTokens.textLg,
          ),
        ),
        // Frame 1049:12590 — 8px under the label, 16px between data lines.
        const SizedBox(height: SimfTokens.space2),
        for (var i = 0; i < lines.length; i++) ...<Widget>[
          if (i != 0) const SizedBox(height: SimfTokens.space4),
          _NumberedLine(index: i + 1, text: lines[i]),
        ],
      ],
    );
  }
}

/// One numbered session-data line — the index sits at the inline start (right
/// in RTL) before the right-aligned beige body, matching the frame's
/// `list-decimal` marker.
class _NumberedLine extends StatelessWidget {
  const _NumberedLine({required this.index, required this.text});

  final int index;
  final String text;

  static const TextStyle _style = TextStyle(
    color: SimfTokens.beigeBorder,
    fontSize: SimfTokens.textMd,
    fontWeight: FontWeight.w500,
    // Frame 1049:12591 — leading ~normal (1.3), tighter than the old 1.5.
    height: 1.3,
  );

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text('$index.', textDirection: TextDirection.ltr, style: _style),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(text, textAlign: TextAlign.start, style: _style),
        ),
      ],
    );
  }
}
