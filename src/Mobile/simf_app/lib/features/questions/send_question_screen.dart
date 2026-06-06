import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/questions_repository.dart';

/// The question recipient — maps to the wire int the API decodes
/// (`SessionQuestionRecipient`: Speaker=0, Host=1).
enum QuestionRecipient {
  speaker,
  host;

  int get wireIndex => index;
}

/// Page 026 — إرسال سؤال · Send a question (#26, `/live/question`).
///
/// **Auth-gated** (route 26 is in `_authenticatedRoutes`). Reached from a live
/// session with the session id in the query string. With no id it shows an
/// "open from a live session" empty state; with an id it shows the form — a
/// recipient choice (Speaker / Host), a multiline question field (max 500), and
/// a Submit that `POST`s `/app/sessions/{id}/questions` (`RequireApprovedAccount`,
/// D-169/D-174). A 400 (`SESSION_NOT_LIVE_FOR_QUESTIONS`) / 404 maps to the
/// "questions are only open around the session" toast; any other failure to a
/// generic error toast. UI is interim — final visuals from SIMF-VID-001.
class SendQuestionScreen extends ConsumerStatefulWidget {
  const SendQuestionScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<SendQuestionScreen> createState() =>
      _SendQuestionScreenState();
}

class _SendQuestionScreenState extends ConsumerState<SendQuestionScreen> {
  final TextEditingController _question = TextEditingController();
  QuestionRecipient _recipient = QuestionRecipient.speaker;
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
    return Scaffold(
      appBar: AppBar(title: Text(l10n.sendQuestionTitle)),
      body: SafeArea(child: _hasSession ? _form(l10n) : _empty(l10n)),
    );
  }

  Widget _empty(AppL10n l10n) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.live_help_outlined,
              size: 56,
              color: SimfTokens.inkMuted,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              l10n.sendQuestionNoSession,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
          ],
        ),
      ),
    );
  }

  Widget _form(AppL10n l10n) {
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        Text(
          l10n.sendQuestionRecipientLabel,
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        SegmentedButton<QuestionRecipient>(
          segments: <ButtonSegment<QuestionRecipient>>[
            ButtonSegment<QuestionRecipient>(
              value: QuestionRecipient.speaker,
              label: Text(l10n.sendQuestionToSpeaker),
              icon: const Icon(Icons.record_voice_over_outlined),
            ),
            ButtonSegment<QuestionRecipient>(
              value: QuestionRecipient.host,
              label: Text(l10n.sendQuestionToHost),
              icon: const Icon(Icons.co_present_outlined),
            ),
          ],
          selected: <QuestionRecipient>{_recipient},
          onSelectionChanged: _submitting
              ? null
              : (selection) => setState(() => _recipient = selection.first),
        ),
        const SizedBox(height: SimfTokens.space4),
        TextField(
          controller: _question,
          maxLength: 500,
          maxLines: 5,
          textInputAction: TextInputAction.newline,
          decoration: InputDecoration(
            labelText: l10n.sendQuestionFieldLabel,
            hintText: l10n.sendQuestionHint,
            errorText: _inlineError,
            border: const OutlineInputBorder(),
          ),
          onChanged: (_) {
            if (_inlineError != null) {
              setState(() => _inlineError = null);
            }
          },
        ),
        const SizedBox(height: SimfTokens.space4),
        FilledButton.icon(
          onPressed: _submitting ? null : () => unawaited(_submit(l10n)),
          icon: const Icon(Icons.send_outlined),
          label: Text(_submitting ? l10n.loadingLabel : l10n.sendQuestionSubmit),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          l10n.sendQuestionWindowHint,
          style: const TextStyle(
            color: SimfTokens.inkMuted,
            fontSize: SimfTokens.textSm,
          ),
        ),
      ],
    );
  }
}
