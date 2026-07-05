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

/// Page 036 — المساعد الذكي · AI assistant (#36, `/chatbot`, Guest+).
///
/// **Public.** Pixel-parity to KSA Figma frame `1064:13066`: the navy
/// [SimfPageShell] shell, a scrolling transcript (assistant bubbles left + gold "AI"
/// badge, user bubbles right + gold fill), the horizontal quick-reply chips
/// (frame `1070:13389`) and the bottom input bar (frame `1070:13398`). The
/// opening transcript is the scripted demo the Figma shows — there is **no
/// backend chatbot endpoint** (verified), so a new prompt (typed or a chip) is
/// echoed as a user bubble and answered by the overridable
/// [chatbotResponderProvider] seam, whose default returns a canned bilingual
/// notice. The screen makes **no API call**.
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

  /// The scripted opening transcript (frame `1064:13066`). Built from l10n each
  /// render so it re-translates on an AR↔EN toggle.
  List<ChatMessage> _seed(AppL10n l10n) => <ChatMessage>[
        ChatMessage(ChatAuthor.assistant, l10n.chatbotGreeting),
        ChatMessage(ChatAuthor.user, l10n.chatbotSeedQ1),
        ChatMessage(ChatAuthor.assistant, l10n.chatbotSeedA1),
        ChatMessage(ChatAuthor.user, l10n.chatbotSeedQ2),
        ChatMessage(ChatAuthor.assistant, l10n.chatbotSeedA2),
      ];

  Future<void> _send(String prompt, bool isArabic) async {
    final text = prompt.trim();
    if (text.isEmpty || _sending) {
      return;
    }
    final responder = ref.read(chatbotResponderProvider);
    setState(() {
      _added.add(ChatMessage(ChatAuthor.user, text));
      _input.clear();
      _sending = true;
    });
    _scrollToEnd();

    final answer = await responder.reply(text, isArabic: isArabic);
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
