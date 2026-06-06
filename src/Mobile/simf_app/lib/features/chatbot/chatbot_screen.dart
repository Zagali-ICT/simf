import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

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
      appBar: AppBar(title: Text(l10n.chatbotTitle)),
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
      color: SimfTokens.field,
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
                color: SimfTokens.inkMuted,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
          IconButton(
            onPressed: onDismiss,
            icon: const Icon(Icons.close, size: 18),
            color: SimfTokens.inkMuted,
            visualDensity: VisualDensity.compact,
          ),
        ],
      ),
    );
  }
}

/// One chat bubble: user → right + accent, assistant → left + field.
class _Bubble extends StatelessWidget {
  const _Bubble({required this.message});

  final _ChatMessage message;

  @override
  Widget build(BuildContext context) {
    final isUser = message.author == _ChatAuthor.user;
    return Align(
      alignment: isUser ? AlignmentDirectional.centerEnd : AlignmentDirectional.centerStart,
      child: Container(
        margin: const EdgeInsets.only(bottom: SimfTokens.space2),
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        constraints: const BoxConstraints(maxWidth: 280),
        decoration: BoxDecoration(
          color: isUser ? SimfTokens.accent : SimfTokens.field,
          borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
        ),
        child: Text(
          message.text,
          style: TextStyle(
            color: isUser ? SimfTokens.surface : SimfTokens.ink,
            fontSize: SimfTokens.textMd,
          ),
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
      child: Row(
        children: <Widget>[
          Expanded(
            child: TextField(
              controller: controller,
              minLines: 1,
              maxLines: 4,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => onSend(),
              decoration: InputDecoration(
                hintText: hint,
                filled: true,
                fillColor: SimfTokens.field,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
                  borderSide: BorderSide.none,
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
                : const Icon(Icons.send),
          ),
        ],
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
            color: SimfTokens.inkMuted,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}
