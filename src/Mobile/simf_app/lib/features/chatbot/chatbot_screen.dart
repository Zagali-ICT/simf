import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart' show SimfBackButton;

/// The seam that turns a user prompt into an assistant reply.
///
/// There is **no backend chatbot endpoint** (verified — see Page_036 README and
/// DECISIONS_LOG). The default implementation is an honest interim stub that
/// returns a fixed bilingual canned message; it never calls the API or the
/// network. When a real assistant provider is procured server-side, swap the
/// implementation behind [chatbotResponderProvider] — the screen stays.
abstract class ChatbotResponder {
  Future<String> reply(String prompt, {required bool isArabic});
}

/// The interim canned responder: every prompt gets the same bilingual notice.
class CannedChatbotResponder implements ChatbotResponder {
  const CannedChatbotResponder();

  @override
  Future<String> reply(String prompt, {required bool isArabic}) async {
    return isArabic
        ? 'المساعد الذكي قيد التفعيل — سيتوفر الرد التلقائي قريباً.'
        : 'The AI assistant is being connected — automatic replies are coming soon.';
  }
}

/// Overridable seam so widget tests inject a fake responder (no network).
final chatbotResponderProvider = Provider<ChatbotResponder>(
  (ref) => const CannedChatbotResponder(),
);

/// Who sent a chat line — drives bubble alignment and colour.
enum _ChatAuthor { user, assistant }

/// One line in the transcript.
class _ChatMessage {
  const _ChatMessage(this.author, this.text);

  final _ChatAuthor author;
  final String text;
}

/// Page 036 — المساعد الذكي · AI assistant (#36, `/chatbot`, Guest+).
///
/// **Public.** Honest **interim** chat shell: a scrolling transcript (user
/// bubbles right, assistant bubbles left) + a bottom input row. On send the
/// user message is appended, then an assistant reply from the overridable
/// [chatbotResponderProvider] seam — whose default returns a fixed bilingual
/// canned notice. There is **no backend chatbot endpoint** (verified), so this
/// screen makes **no API call**. A one-time preview banner sits at the top. UI
/// is interim (final visuals from SIMF-VID-001).
class ChatbotScreen extends ConsumerStatefulWidget {
  const ChatbotScreen({super.key});

  @override
  ConsumerState<ChatbotScreen> createState() => _ChatbotScreenState();
}

class _ChatbotScreenState extends ConsumerState<ChatbotScreen> {
  final TextEditingController _input = TextEditingController();
  final ScrollController _scroll = ScrollController();
  final List<_ChatMessage> _messages = <_ChatMessage>[];
  bool _sending = false;
  bool _bannerVisible = true;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send(bool isArabic) async {
    final prompt = _input.text.trim();
    if (prompt.isEmpty || _sending) {
      return;
    }
    final responder = ref.read(chatbotResponderProvider);
    setState(() {
      _messages.add(_ChatMessage(_ChatAuthor.user, prompt));
      _input.clear();
      _sending = true;
    });
    _scrollToEnd();

    final answer = await responder.reply(prompt, isArabic: isArabic);
    if (!mounted) {
      return;
    }
    setState(() {
      _messages.add(_ChatMessage(_ChatAuthor.assistant, answer));
      _sending = false;
    });
    _scrollToEnd();
  }

  void _scrollToEnd() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.jumpTo(_scroll.position.maxScrollExtent);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        leading: const SimfBackButton(),
        title: Text(l10n.chatbotTitle),
      ),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            if (_bannerVisible)
              _PreviewBanner(
                message: l10n.chatbotPreviewBanner,
                onDismiss: () => setState(() => _bannerVisible = false),
              ),
            Expanded(
              child: _messages.isEmpty
                  ? _Empty(message: l10n.chatbotEmpty)
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.all(SimfTokens.space4),
                      itemCount: _messages.length,
                      itemBuilder: (_, index) =>
                          _Bubble(message: _messages[index]),
                    ),
            ),
            _Composer(
              controller: _input,
              hint: l10n.chatbotInputHint,
              sendTooltip: l10n.chatbotSendTooltip,
              sending: _sending,
              onSend: () => unawaited(_send(l10n.isArabic)),
            ),
          ],
        ),
      ),
    );
  }
}

/// The dismissible one-time notice that the assistant is in preview.
class _PreviewBanner extends StatelessWidget {
  const _PreviewBanner({required this.message, required this.onDismiss});

  final String message;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      decoration: const BoxDecoration(
        color: SimfTokens.surfaceTint,
        border: Border(bottom: BorderSide(color: SimfTokens.line2)),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space3,
      ),
      child: Row(
        children: <Widget>[
          const Icon(
            Icons.info_outline,
            size: 18,
            color: SimfTokens.accent,
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                color: SimfTokens.txtSecondary,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
          IconButton(
            onPressed: onDismiss,
            icon: const Icon(Icons.close, size: 18),
            color: SimfTokens.txtTertiary,
            visualDensity: VisualDensity.compact,
          ),
        ],
      ),
    );
  }
}

/// One chat bubble: user → right + accent + navy text, assistant → left +
/// navy-surface fill + line2 hairline + surface text, prefixed by an "AI" pill.
class _Bubble extends StatelessWidget {
  const _Bubble({required this.message});

  final _ChatMessage message;

  @override
  Widget build(BuildContext context) {
    final isUser = message.author == _ChatAuthor.user;
    final text = Text(
      message.text,
      style: TextStyle(
        color: isUser ? SimfTokens.navy : SimfTokens.surface,
        fontSize: SimfTokens.textSm,
        height: 1.55,
        fontWeight: isUser ? FontWeight.w600 : FontWeight.w400,
      ),
    );
    return Align(
      alignment: isUser
          ? AlignmentDirectional.centerEnd
          : AlignmentDirectional.centerStart,
      child: Container(
        margin: const EdgeInsets.only(bottom: SimfTokens.space2),
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        constraints: const BoxConstraints(maxWidth: 280),
        decoration: BoxDecoration(
          color: isUser ? SimfTokens.accent : SimfTokens.surfaceTint,
          border: isUser ? null : Border.all(color: SimfTokens.line2),
          borderRadius: BorderRadiusDirectional.only(
            topStart: const Radius.circular(SimfTokens.radiusLarge),
            topEnd: const Radius.circular(SimfTokens.radiusLarge),
            bottomStart: Radius.circular(
              isUser ? SimfTokens.radiusSmall : SimfTokens.radiusLarge,
            ),
            bottomEnd: Radius.circular(
              isUser ? SimfTokens.radiusLarge : SimfTokens.radiusSmall,
            ),
          ),
        ),
        child: isUser
            ? text
            : Row(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const _AiPill(),
                  const SizedBox(width: SimfTokens.space2),
                  Flexible(child: text),
                ],
              ),
      ),
    );
  }
}

/// The small "AI" tag the mockup prefixes every assistant bubble with.
class _AiPill extends StatelessWidget {
  const _AiPill();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space1,
        vertical: 1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: const Text(
        'AI',
        style: TextStyle(
          color: SimfTokens.navy,
          fontSize: 8.5,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.4,
        ),
      ),
    );
  }
}

/// The bottom input row: a text field + a send button (spinner while sending).
class _Composer extends StatelessWidget {
  const _Composer({
    required this.controller,
    required this.hint,
    required this.sendTooltip,
    required this.sending,
    required this.onSend,
  });

  final TextEditingController controller;
  final String hint;
  final String sendTooltip;
  final bool sending;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space3),
      child: Container(
        decoration: BoxDecoration(
          color: SimfTokens.surfaceTint,
          border: Border.all(color: SimfTokens.line2),
          borderRadius: BorderRadius.circular(999),
        ),
        padding: const EdgeInsetsDirectional.only(
          start: SimfTokens.space4,
          end: SimfTokens.space1,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: TextField(
                controller: controller,
                minLines: 1,
                maxLines: 4,
                textInputAction: TextInputAction.send,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textSm,
                ),
                onSubmitted: (_) => onSend(),
                decoration: InputDecoration(
                  hintText: hint,
                  hintStyle: const TextStyle(
                    color: SimfTokens.txtTertiary,
                    fontSize: SimfTokens.textSm,
                  ),
                  isCollapsed: true,
                  filled: false,
                  border: InputBorder.none,
                  contentPadding: const EdgeInsets.symmetric(
                    vertical: SimfTokens.space2,
                  ),
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            IconButton.filled(
              tooltip: sendTooltip,
              onPressed: sending ? null : onSend,
              icon: sending
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send, size: 18),
            ),
          ],
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.smart_toy_outlined,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtTertiary)),
        ],
      ),
    );
  }
}
