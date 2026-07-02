import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../sessions/data/session_detail_repository.dart';
import '../sessions/data/session_models.dart';
import 'data/questions_repository.dart';

/// The question recipient — maps to the wire int the API decodes
/// (`SessionQuestionRecipient`: Speaker=0, Host=1).
enum QuestionRecipient {
  speaker,
  host;

  int get wireIndex => index;
}

/// Page 026 — معلومات عن الجلسة · Session information + ask a question (#26,
/// `/live/question`), rebuilt to the KSA-Project Figma frame **934:3636** on the
/// shared shell.
///
/// **Auth-gated** (route 26 is in `_authenticatedRoutes`). Reached from a live
/// session with the session id in the query string. With no id it shows an
/// "open from a live session" empty state; with an id it shows the frame: the
/// **"بيانات الجلسة"** session-data block (the session description rendered as a
/// numbered list, frame `1049:12590`) over the **"الاسئلة"** composer — a tinted
/// borderless multiline question box (frame `934:3668`, max 500), the gold
/// full-width submit, and the centred gold-bulleted "reviewed before air" note
/// (frame `943:3750`).
///
/// The session-data block reads the **anonymous** detail
/// (`GET /app/programme/sessions/{id}` — the same shipped endpoint the session
/// detail / live screens use, no new API). It is **non-blocking context**: a
/// fetch failure just hides the block and the composer still works.
///
/// The frame shows no recipient selector, so the form submits to the default
/// recipient (Speaker = 0); the submit API + `recipient` wire field are
/// preserved (`POST /app/sessions/{id}/questions`, `RequireApprovedAccount`,
/// D-169/D-174). A 400 (`SESSION_NOT_LIVE_FOR_QUESTIONS`) / 404 maps to the
/// "questions are only open around the session" toast; any other failure to a
/// generic error toast.
class SendQuestionScreen extends ConsumerStatefulWidget {
  const SendQuestionScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<SendQuestionScreen> createState() =>
      _SendQuestionScreenState();
}

class _SendQuestionScreenState extends ConsumerState<SendQuestionScreen> {
  final TextEditingController _question = TextEditingController();
  // The frame carries no recipient selector; the question is submitted to the
  // default recipient. The wire `recipient` field is preserved (D-169/D-174).
  static const QuestionRecipient _recipient = QuestionRecipient.speaker;
  bool _submitting = false;
  String? _inlineError;

  /// The session whose data fills the "بيانات الجلسة" block, or null while it
  /// loads / when the optional read fails (the composer is unaffected).
  SessionDetail? _detail;

  bool get _hasSession =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    if (_hasSession) {
      unawaited(_loadDetail());
    }
  }

  /// Loads the session detail for the "بيانات الجلسة" block. Non-blocking
  /// context: an [ApiFailure] just leaves the block hidden — the composer below
  /// still works.
  Future<void> _loadDetail() async {
    try {
      final detail = await ref
          .read(sessionDetailRepositoryProvider)
          .getDetail(widget.sessionId!.trim());
      if (!mounted) {
        return;
      }
      setState(() => _detail = detail);
    } on ApiFailure {
      // Optional block — ignore; the question form is the primary function.
    }
  }

  /// Splits the session description into the frame's numbered data lines
  /// (frame 1049:12591-12594): one entry per non-blank line, in order. A single
  /// paragraph renders as one numbered item; a blank description hides the block.
  static List<String> _dataLines(String? description) {
    final text = description?.trim() ?? '';
    if (text.isEmpty) {
      return const <String>[];
    }
    return text
        .split('\n')
        .map((line) => line.trim())
        .where((line) => line.isNotEmpty)
        .toList(growable: false);
  }

  @override
  void dispose() {
    _question.dispose();
    super.dispose();
  }

  Future<void> _submit(AppL10n l10n) async {
    final text = _question.text.trim();
    if (text.isEmpty) {
      setState(() => _inlineError = l10n.sendQuestionEmpty);
      return;
    }
    setState(() {
      _inlineError = null;
      _submitting = true;
    });
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(questionsRepositoryProvider).submitQuestion(
            widget.sessionId!.trim(),
            questionText: text,
            recipientIndex: _recipient.wireIndex,
          );
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _question.clear();
      });
      messenger.showSnackBar(SnackBar(content: Text(l10n.sendQuestionSent)));
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      final notOpen = failure.code == 'SESSION_NOT_LIVE_FOR_QUESTIONS' ||
          failure.httpStatus == 404;
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            notOpen ? l10n.sendQuestionNotOpen : l10n.sendQuestionFailed,
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.sessionInfoTitle,
      onBack: () => backOrHome(context),
      body: _hasSession ? _form(l10n) : _empty(l10n),
    );
  }

  Widget _empty(AppL10n l10n) {
    return SimfEmptyState(
      icon: Icons.live_help_outlined,
      message: l10n.sendQuestionNoSession,
    );
  }

  Widget _form(AppL10n l10n) {
    final detail = _detail;
    final dataLines = detail == null
        ? const <String>[]
        : _dataLines(detail.localizedDescription(l10n.isArabic));
    // Frame 934:3636 — the data block + composer occupy the top (scrollable so a
    // long description + the keyboard never overflow), and the submit + note are
    // pinned to the bottom of the screen (943:3751), not flowed under the box.
    return Column(
      children: <Widget>[
        Expanded(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space5,
              SimfTokens.space4,
              SimfTokens.space4,
            ),
            children: <Widget>[
              // Frame 1049:12590 — the "بيانات الجلسة" session-data block over
              // the composer. Hidden until the optional detail read lands.
              if (dataLines.isNotEmpty) ...<Widget>[
                _SessionDataBlock(
                  label: l10n.sessionDataLabel,
                  lines: dataLines,
                ),
                const SizedBox(height: SimfTokens.space6),
              ],
              // Frame 945:3756 — the "الاسئلة" section label: white, Medium,
              // aligned to the inline end (right in RTL).
              Text(
                l10n.sendQuestionSectionLabel,
                // TextAlign.start = right under RTL (TextAlign.end would be left).
                textAlign: TextAlign.start,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w500,
                  fontSize: SimfTokens.textLg,
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              // Frame 934:3668 — the fixed 100px tinted question box: navyDeep
              // fill on the 8px radius (no border), placeholder pinned to the
              // top, beige + inline-end aligned.
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
                  controller: _question,
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
                    hintText: l10n.sendQuestionHint,
                    hintStyle: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                    errorText: _inlineError,
                    errorStyle: const TextStyle(color: SimfTokens.danger),
                  ),
                  onChanged: (_) {
                    if (_inlineError != null) {
                      setState(() => _inlineError = null);
                    }
                  },
                ),
              ),
            ],
          ),
        ),
        // Frame 943:3751 — the bottom-pinned submit + reviewed-before-air note.
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              // Frame 942:3746 — the gold full-width submit: white SemiBold label
              // on the 4px-radius accent fill.
              SizedBox(
                width: double.infinity,
                height: 44,
                child: FilledButton(
                  onPressed:
                      _submitting ? null : () => unawaited(_submit(l10n)),
                  style: FilledButton.styleFrom(
                    backgroundColor: SimfTokens.accent,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                    ),
                    textStyle: const TextStyle(
                      fontSize: SimfTokens.textSm,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  child: Text(
                    _submitting ? l10n.loadingLabel : l10n.sendQuestionSubmit,
                  ),
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              // Frame 943:3750 — the centred bulleted note: "ملاحظة" gold/SemiBold,
              // "سيتم مراجعته قبل العرض المباشر" beige.
              _ReviewNote(
                label: l10n.sendQuestionNoteLabel,
                body: l10n.sendQuestionWindowHint,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// The frame 943:3750 footnote — a single centred gold bullet, the bold gold
/// "ملاحظة" word, then the muted-beige "reviewed before air" body.
class _ReviewNote extends StatelessWidget {
  const _ReviewNote({required this.label, required this.body});

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

/// The frame 1049:12590 "بيانات الجلسة" block: the white Medium section header
/// over the session-data lines rendered as a right-aligned numbered list
/// (frame 1049:12591-12594), each line `#C2B8A2` 14px Medium.
class _SessionDataBlock extends StatelessWidget {
  const _SessionDataBlock({required this.label, required this.lines});

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
