import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/chatbot/data/ai_chat_history_repository.dart';
import 'package:simf_app/features/chatbot/data/chat_message.dart';
import 'package:simf_app/features/chatbot/data/chatbot_responder.dart';
import 'package:simf_app/features/chatbot/widgets/chat_bubble.dart';
import 'package:simf_app/features/chatbot/widgets/chat_composer.dart';
import 'package:simf_app/features/chatbot/widgets/quick_replies.dart';

// The responder seam lives in `data/`; re-exported so the existing
// `ChatbotResponder` / `chatbotResponderProvider` imports (the chatbot test
// injects a fake responder) keep resolving off this screen.
export 'data/chatbot_responder.dart';

/// AI assistant — route: `RouteNames.chatbot` · Figma 1064:13066
class ChatbotScreen extends ConsumerStatefulWidget {
  const ChatbotScreen({super.key});

  @override
  ConsumerState<ChatbotScreen> createState() => _ChatbotScreenState();
}

class _ChatbotScreenState extends ConsumerState<ChatbotScreen> {
  final TextEditingController _input = TextEditingController();
  final ScrollController _scroll = ScrollController();

  /// User/assistant lines added THIS session, shown after the greeting and the
  /// loaded history. The server persists each turn (via the assistance
  /// endpoint), so they reload as history on the next visit.
  final List<ChatMessage> _added = <ChatMessage>[];
  bool _sending = false;

  /// The saved transcript frozen at the first send. Before any send the live
  /// [aiChatHistoryProvider] snapshot is shown; freezing on the first send
  /// stops a late-resolving history load — which by then already includes the
  /// just-sent turn — from double-rendering it alongside [_added].
  List<ChatMessage>? _openedHistory;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  /// The assistant's opening greeting (frame `1064:13066`). Built from l10n
  /// each render so it re-translates on an AR↔EN toggle.
  List<ChatMessage> _seed(AppL10n l10n) => <ChatMessage>[
        ChatMessage(ChatAuthor.assistant, l10n.chatbotGreeting),
      ];

  Future<void> _send(String prompt, bool isArabic) async {
    final text = prompt.trim();
    if (text.isEmpty || _sending) {
      return;
    }
    // Resolved before the await so the error fallback needs no post-await
    // context.
    final errorReply = AppL10n.of(context).chatbotError;
    final responder = ref.read(chatbotResponderProvider);
    // Freeze the open-time history snapshot on the first send (see the field).
    _openedHistory ??= ref.read(aiChatHistoryProvider).maybeWhen(
          data: (history) => history,
          orElse: () => const <ChatMessage>[],
        );
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
    // The saved transcript loaded at open (survives navigation/restart; the
    // assistant also remembers it server-side). Best-effort: empty while
    // loading or on a wire error, so a history failure never blanks the chat —
    // the greeting still shows and the composer still works. Frozen to the
    // open-time snapshot once the user has sent (see [_openedHistory]).
    final loaded = ref.watch(aiChatHistoryProvider).maybeWhen(
          data: (history) => history,
          orElse: () => const <ChatMessage>[],
        );
    final saved = _openedHistory ?? loaded;
    final messages = <ChatMessage>[..._seed(l10n), ...saved, ..._added];
    return SimfPageShell(
      title: l10n.chatbotTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: SimfPullToRefresh(
              onRefresh: () => refreshAsync(ref, aiChatHistoryProvider.future),
              child: ListView.builder(
                controller: _scroll,
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(SimfTokens.space4),
                itemCount: messages.length,
                itemBuilder: (_, index) => ChatBubble(message: messages[index]),
              ),
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
