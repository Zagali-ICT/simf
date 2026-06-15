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
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.sendQuestionTitle)),
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
              color: SimfTokens.txtTertiary,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              l10n.sendQuestionNoSession,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.txtTertiary),
            ),
          ],
        ),
      ),
    );
  }

  Widget _form(AppL10n l10n) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        // Mockup `.recipient-row .label` — quiet 10.5px caption above the
        // recipient toggle.
        Text(
          l10n.sendQuestionRecipientLabel,
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textXs,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        // Mockup `.recipient-row` — two equal-width radio pills.
        Row(
          children: <Widget>[
            Expanded(
              child: _RecipientPill(
                label: l10n.sendQuestionToSpeaker,
                selected: _recipient == QuestionRecipient.speaker,
                onTap: _submitting
                    ? null
                    : () => setState(
                          () => _recipient = QuestionRecipient.speaker,
                        ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _RecipientPill(
                label: l10n.sendQuestionToHost,
                selected: _recipient == QuestionRecipient.host,
                onTap: _submitting
                    ? null
                    : () => setState(
                          () => _recipient = QuestionRecipient.host,
                        ),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space4),
        // Mockup `.q-box` — themed multiline field (tinted fill + hairline).
        TextField(
          controller: _question,
          maxLength: 500,
          maxLines: 5,
          textInputAction: TextInputAction.newline,
          decoration: InputDecoration(
            labelText: l10n.sendQuestionFieldLabel,
            hintText: l10n.sendQuestionHint,
            errorText: _inlineError,
          ),
          onChanged: (_) {
            if (_inlineError != null) {
              setState(() => _inlineError = null);
            }
          },
        ),
        const SizedBox(height: SimfTokens.space4),
        // Mockup `.q-send` — gold/navy primary action.
        FilledButton.icon(
          onPressed: _submitting ? null : () => unawaited(_submit(l10n)),
          icon: const Icon(Icons.send_outlined),
          label: Text(_submitting ? l10n.loadingLabel : l10n.sendQuestionSubmit),
        ),
        const SizedBox(height: SimfTokens.space3),
        // Mockup `.q-note` — quiet "reviewed before air" footnote.
        Text(
          l10n.sendQuestionWindowHint,
          textAlign: TextAlign.right,
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontSize: SimfTokens.textXs,
            height: 1.6,
          ),
        ),
      ],
    );
  }
}

/// One recipient choice (mockup `.recipient` / `.recipient.on`): a full-width
/// pill with a leading radio dot that fills gold when selected. Selected =
/// accent border + accent-8% fill + white text; unselected = hairline border +
/// tinted fill + secondary text.
class _RecipientPill extends StatelessWidget {
  const _RecipientPill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space2,
          ),
          decoration: BoxDecoration(
            color: selected
                ? SimfTokens.accent.withValues(alpha: 0.08)
                : SimfTokens.surfaceTint,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.line,
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              _RadioDot(selected: selected),
              const SizedBox(width: SimfTokens.space2),
              Text(
                label,
                style: TextStyle(
                  color: selected
                      ? SimfTokens.surface
                      : SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textXs,
                  fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The mockup `.recipient::before` dot — a 10px ring that fills gold when on.
class _RadioDot extends StatelessWidget {
  const _RadioDot({required this.selected});

  final bool selected;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 12,
      height: 12,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: selected ? SimfTokens.accent : Colors.transparent,
        border: Border.all(
          color: selected ? SimfTokens.accent : SimfTokens.txtTertiary,
          width: 1.5,
        ),
      ),
    );
  }
}
