import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/questions_repository.dart';

/// The question recipient — maps to the wire int the API decodes
/// (`SessionQuestionRecipient`: Speaker=0, Host=1).
enum QuestionRecipient {
  speaker,
  host;

  int get wireIndex => index;
}

/// Page 026 — إرسال سؤال · Send a question (#26, `/live/question`), rebuilt to
/// the KSA-Project Figma frame **934:3636 "Live Video"** (the ask-a-question
/// form portion) on the shared shell.
///
/// **Auth-gated** (route 26 is in `_authenticatedRoutes`). Reached from a live
/// session with the session id in the query string. With no id it shows an
/// "open from a live session" empty state; with an id it shows the form — the
/// "الاسئلة" label, a tinted multiline question box (frame `934:3668`, max 500),
/// the gold full-width "ارسال السؤال" submit, and the centred gold-bulleted
/// "reviewed before air" note (frame `943:3750`).
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

  bool get _hasSession =>
      widget.sessionId != null && widget.sessionId!.trim().isNotEmpty;

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
    return KsaPage(
      title: l10n.sendQuestionTitle,
      onBack: () => ksaBackOrHome(context),
      body: _hasSession ? _form(l10n) : _empty(l10n),
    );
  }

  Widget _empty(AppL10n l10n) {
    return KsaEmptyState(
      icon: Icons.live_help_outlined,
      message: l10n.sendQuestionNoSession,
    );
  }

  Widget _form(AppL10n l10n) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space5,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        // Frame 945:3756 — the "الاسئلة" section label: white, Medium, aligned
        // to the inline end (right in RTL).
        Text(
          l10n.sendQuestionSectionLabel,
          textAlign: TextAlign.end,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w500,
            fontSize: SimfTokens.textLg,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        // Frame 934:3668 — the tinted multiline question box: navyDeep fill on
        // the 8px radius, a faint beige 0.2 hairline border, the placeholder beige and inline-end aligned.
        Container(
          decoration: BoxDecoration(
            color: SimfTokens.navyDeep,
            borderRadius:
                BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space3,
          ),
          child: TextField(
            controller: _question,
            maxLength: 500,
            minLines: 4,
            maxLines: 6,
            textAlign: TextAlign.right,
            textInputAction: TextInputAction.newline,
            style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textSm),
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
        const SizedBox(height: SimfTokens.space6),
        // Frame 942:3746 — the gold full-width submit: white SemiBold label on
        // the 4px-radius accent fill.
        SizedBox(
          height: 44,
          child: FilledButton(
            onPressed: _submitting ? null : () => unawaited(_submit(l10n)),
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                borderRadius:
                    BorderRadius.circular(SimfTokens.radiusSmall),
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
