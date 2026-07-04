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
import 'widgets/send_question_content.dart';

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
                SessionDataBlock(
                  label: l10n.sessionDataLabel,
                  lines: dataLines,
                ),
                const SizedBox(height: SimfTokens.space6),
              ],
              SendQuestionComposer(
                sectionLabel: l10n.sendQuestionSectionLabel,
                hint: l10n.sendQuestionHint,
                controller: _question,
                errorText: _inlineError,
                onChanged: (_) {
                  if (_inlineError != null) {
                    setState(() => _inlineError = null);
                  }
                },
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
              SendQuestionSubmitButton(
                label: _submitting ? l10n.loadingLabel : l10n.sendQuestionSubmit,
                onPressed: _submitting ? null : () => unawaited(_submit(l10n)),
              ),
              const SizedBox(height: SimfTokens.space4),
              // Frame 943:3750 — the centred bulleted note: "ملاحظة" gold/SemiBold,
              // "سيتم مراجعته قبل العرض المباشر" beige.
              ReviewNote(
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
