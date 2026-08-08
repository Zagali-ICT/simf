import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/validation/field_limits.dart';
import '../../../core/widgets/simf_radio_pill.dart';

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
            style: SimfTokens.textAccent,
          ),
          TextSpan(
            text: '$label ',
            style: SimfTokens.labelGoldSemiboldLg,
          ),
          TextSpan(
            text: body,
            style: SimfTokens.bodyBeigeMd,
          ),
        ],
      ),
      textAlign: TextAlign.center,
    );
  }
}

/// B7 — the "إلى من؟" recipient choice (D-174: the mockup's two pills,
/// المتحدث / المضيف). The wire field and the moderator + committee queues have
/// carried `Recipient` since D-169, but the screen hardcoded Speaker, so every
/// question ever submitted read `recipient = 0` and the Host half of the enum
/// — plus these three strings — was dead. Built from the shared
/// [SimfRadioPill], not a page-local pill.
class SendQuestionRecipientPicker extends StatelessWidget {
  const SendQuestionRecipientPicker({
    required this.label,
    required this.speakerLabel,
    required this.hostLabel,
    required this.hostSelected,
    required this.onSpeaker,
    required this.onHost,
    super.key,
  });

  final String label;
  final String speakerLabel;
  final String hostLabel;

  /// True when the Host pill is the current choice; false = Speaker (the
  /// default, preserving the shipped behaviour for a user who never taps).
  final bool hostSelected;
  final VoidCallback onSpeaker;
  final VoidCallback onHost;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // TextAlign.start = right under RTL, matching the composer's label.
        Text(
          label,
          textAlign: TextAlign.start,
          style: SimfTokens.labelWhiteMediumLg,
        ),
        const SizedBox(height: SimfTokens.space2),
        Row(
          children: <Widget>[
            Expanded(
              child: SimfRadioPill(
                label: speakerLabel,
                selected: !hostSelected,
                onTap: onSpeaker,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: SimfRadioPill(
                label: hostLabel,
                selected: hostSelected,
                onTap: onHost,
              ),
            ),
          ],
        ),
      ],
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
          style: SimfTokens.labelWhiteMediumLg,
        ),
        const SizedBox(height: SimfTokens.space2),
        Container(
          height: SimfTokens.questionBoxHeight,
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
            maxLength: FieldLimits.sessionQuestion,
            maxLines: null,
            expands: true,
            textAlign: TextAlign.start,
            textAlignVertical: TextAlignVertical.top,
            textInputAction: TextInputAction.newline,
            style: SimfTokens.bodyWhiteSm,
            cursorColor: SimfTokens.accent,
            decoration: InputDecoration(
              isCollapsed: true,
              border: InputBorder.none,
              counterText: '',
              hintText: hint,
              hintStyle: SimfTokens.labelBeigeSm,
              errorText: errorText,
              errorStyle: SimfTokens.bodyDanger,
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
      height: SimfTokens.tapTarget,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
        child: Text(
          label,
          style: SimfTokens.labelSemiboldSm,
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
          style: SimfTokens.labelWhiteMediumLg,
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

  // Frame 1049:12591 — leading 1.3, tighter than the 1.5 body.
  static const TextStyle _style = SimfTokens.bodyBeigeMedium13;

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
