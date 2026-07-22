import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/chat_message.dart';
import 'data/chatbot_responder.dart';
import 'widgets/chat_bubble.dart';
import 'widgets/chat_composer.dart';
import 'widgets/quick_replies.dart';

// The responder seam lives in `data/`; re-exported so the existing
// `ChatbotResponder` / `chatbotResponderProvider` imports (the chatbot test
// injects a fake responder) keep resolving off this screen.
export 'data/chatbot_responder.dart';

/// Page 036 — المساعد الذكي · AI assistant (#36, `/chatbot`, signed-in).
///
/// Pixel-parity to KSA Figma frame `1064:13066`: the navy [SimfPageShell] shell, a
/// scrolling transcript (assistant bubbles left + gold "AI" badge, user bubbles
/// right + gold fill), the horizontal quick-reply chips (frame `1070:13389`) and
/// the bottom input bar (frame `1070:13398`). The screen opens with the assistant
/// greeting; each prompt (typed or a chip) is answered by the centralised AI via
/// the overridable [chatbotResponderProvider] — its default [ApiChatbotResponder]
/// calls `POST /app/ai/assistance` (the `assistance` prompt, grounded server-side
/// on the live event context). A wire error surfaces as a localized error bubble.
class ChatbotScreen extends ConsumerStatefulWidget {
  const ChatbotScreen({super.key});

  @override
  ConsumerState<ChatbotScreen> createState() => _ChatbotScreenState();
}

class _ChatbotScreenState extends ConsumerState<ChatbotScreen> {
  final TextEditingController _input = TextEditingController();
  final ScrollController _scroll = ScrollController();

  /// User/assistant lines added after the scripted opening transcript.
  final List<ChatMessage> _added = <ChatMessage>[];
  bool _sending = false;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  /// The assistant's opening greeting (frame `1064:13066`). Built from l10n each
  /// render so it re-translates on an AR↔EN toggle.
  List<ChatMessage> _seed(AppL10n l10n) => <ChatMessage>[
        ChatMessage(ChatAuthor.assistant, l10n.chatbotGreeting),
      ];

  Future<void> _send(String prompt, bool isArabic) async {
    final text = prompt.trim();
    if (text.isEmpty || _sending) {
      return;
    }
    // Resolved before the await so the error fallback needs no post-await context.
    final errorReply = AppL10n.of(context).chatbotError;
    final responder = ref.read(chatbotResponderProvider);
    setState(() {
      _added.add(ChatMessage(ChatAuthor.user, text));
      _input.clear();
      _sending = true;
    });
    _scrollToEnd();

    // A failed (or empty) AI call becomes the localized error bubble, and
    // `_sending` is always cleared in the setState below — a failed send must
    // never leave the composer spinner stuck. reply() can throw beyond
    // ApiFailure (e.g. a token-refresh / keystore error on the 401 path), so
    // catch every throwable and fall back to the error bubble.
    var answer = errorReply;
    try {
      final reply = await responder.reply(text, isArabic: isArabic);
      if (reply.isNotEmpty) {
        answer = reply;
      }
    } on Object catch (_) {
      // Keep the error bubble for any failure.
    }
    if (!mounted) {
      return;
    }
    setState(() {
      _added.add(ChatMessage(ChatAuthor.assistant, answer));
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
    final messages = <ChatMessage>[..._seed(l10n), ..._added];
    return SimfPageShell(
      title: l10n.chatbotTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: ListView.builder(
              controller: _scroll,
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: messages.length,
              itemBuilder: (_, index) => ChatBubble(message: messages[index]),
            ),
          ),
          QuickReplies(
            labels: <String>[
              l10n.chatbotChipMeeting,
              l10n.chatbotChipUpcoming,
              l10n.chatbotChipSami,
              l10n.chatbotChipToday,
            ],
            onTap: (label) => unawaited(_send(label, l10n.isArabic)),
          ),
          const SizedBox(height: SimfTokens.space3),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              0,
              SimfTokens.space4,
              SimfTokens.space3,
            ),
            child: ChatComposer(
              controller: _input,
              hint: l10n.chatbotInputHint,
              sendTooltip: l10n.chatbotSendTooltip,
              sending: _sending,
              onSend: () => unawaited(_send(_input.text, l10n.isArabic)),
            ),
          ),
        ],
      ),
    );
  }
}
