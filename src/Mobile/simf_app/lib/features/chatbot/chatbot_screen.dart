import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

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
/// **Public.** Pixel-parity to KSA Figma frame `1064:13066`: the navy
/// [KsaPage] shell, a scrolling transcript (assistant bubbles left + gold "AI"
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
  final List<_ChatMessage> _added = <_ChatMessage>[];
  bool _sending = false;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  /// The scripted opening transcript (frame `1064:13066`). Built from l10n each
  /// render so it re-translates on an AR↔EN toggle.
  List<_ChatMessage> _seed(AppL10n l10n) => <_ChatMessage>[
        _ChatMessage(_ChatAuthor.assistant, l10n.chatbotGreeting),
        _ChatMessage(_ChatAuthor.user, l10n.chatbotSeedQ1),
        _ChatMessage(_ChatAuthor.assistant, l10n.chatbotSeedA1),
        _ChatMessage(_ChatAuthor.user, l10n.chatbotSeedQ2),
        _ChatMessage(_ChatAuthor.assistant, l10n.chatbotSeedA2),
      ];

  Future<void> _send(String prompt, bool isArabic) async {
    final text = prompt.trim();
    if (text.isEmpty || _sending) {
      return;
    }
    final responder = ref.read(chatbotResponderProvider);
    setState(() {
      _added.add(_ChatMessage(_ChatAuthor.user, text));
      _input.clear();
      _sending = true;
    });
    _scrollToEnd();

    final answer = await responder.reply(text, isArabic: isArabic);
    if (!mounted) {
      return;
    }
    setState(() {
      _added.add(_ChatMessage(_ChatAuthor.assistant, answer));
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
    final messages = <_ChatMessage>[..._seed(l10n), ..._added];
    return KsaPage(
      title: l10n.chatbotTitle,
      onBack: () => ksaBackOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: ListView.builder(
              controller: _scroll,
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: messages.length,
              itemBuilder: (_, index) => _Bubble(message: messages[index]),
            ),
          ),
          _QuickReplies(
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
            child: _Composer(
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

/// One chat bubble — pinned to match the Figma regardless of locale: assistant
/// bubbles to the left (navy-deep fill + a top-end gold "AI" badge, frame
/// `1064:13275`), user bubbles to the right (gold fill, frame `1064:13280`).
/// The small 2px corner is the inner-bottom tail in each case.
class _Bubble extends StatelessWidget {
  const _Bubble({required this.message});

  final _ChatMessage message;

  static const Radius _r = Radius.circular(SimfTokens.radius); // 8 — large corners
  static const Radius _tail = Radius.circular(2); // Figma bubble tail

  @override
  Widget build(BuildContext context) {
    final isUser = message.author == _ChatAuthor.user;
    final text = Text(
      message.text,
      style: TextStyle(
        color: isUser ? Colors.white : SimfTokens.chatBubbleText,
        fontSize: SimfTokens.textMd,
        height: 1.5,
        fontWeight: isUser ? FontWeight.w600 : FontWeight.w400,
      ),
    );
    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: SimfTokens.space3),
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3 + 3, // ≈15 (frame text inset)
          vertical: SimfTokens.space3,
        ),
        constraints: const BoxConstraints(maxWidth: 288),
        decoration: BoxDecoration(
          color: isUser ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.only(
            topLeft: _r,
            topRight: _r,
            bottomLeft: isUser ? _tail : _r,
            bottomRight: isUser ? _r : _tail,
          ),
        ),
        child: isUser
            ? text
            : Row(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const _AiBadge(),
                  const SizedBox(width: SimfTokens.space2),
                  Flexible(child: text),
                ],
              ),
      ),
    );
  }
}

/// The gold "AI" tag prefixing every assistant bubble (frame `1064:13276`):
/// a gold pill, white bold "AI" at 12px.
class _AiBadge extends StatelessWidget {
  const _AiBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: const Text(
        'AI',
        style: TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm,
          height: 16 / 12,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

/// The horizontal quick-reply chip strip (frame `1070:13389`): beige-hairline
/// pills, beige 12px SemiBold text, scrolls past the screen edge. Tapping one
/// sends it as the next prompt.
class _QuickReplies extends StatelessWidget {
  const _QuickReplies({required this.labels, required this.onTap});

  final List<String> labels;
  final ValueChanged<String> onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 34,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
        itemCount: labels.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (_, index) => _QuickReplyChip(
          label: labels[index],
          onTap: () => onTap(labels[index]),
        ),
      ),
    );
  }
}

class _QuickReplyChip extends StatelessWidget {
  const _QuickReplyChip({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
        decoration: BoxDecoration(
          border: Border.all(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
            height: 18 / 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

/// The bottom input bar (frame `1070:13398`): a navy-deep bar with the beige
/// hairline, the placeholder at the inline end and the gold send square at the
/// inline start (a spinner replaces the glyph while sending).
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
    return Container(
      constraints: const BoxConstraints(minHeight: 48),
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
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
                color: Colors.white,
                fontSize: SimfTokens.textSm,
              ),
              onSubmitted: (_) => onSend(),
              decoration: InputDecoration(
                hintText: hint,
                hintStyle: const TextStyle(
                  color: Colors.white,
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
          Semantics(
            button: true,
            label: sendTooltip,
            child: InkWell(
              onTap: sending ? null : onSend,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              child: Container(
                width: 24,
                height: 24,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
                child: sending
                    ? const SizedBox(
                        width: 12,
                        height: 12,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.send, size: 14, color: Colors.white),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
