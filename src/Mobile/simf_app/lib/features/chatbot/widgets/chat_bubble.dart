import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/chat_message.dart';

/// One chat bubble — pinned to match the Figma regardless of locale: assistant
/// bubbles to the left (navy-deep fill + a top-end gold "AI" badge, frame
/// `1064:13275`), user bubbles to the right (gold fill, frame `1064:13280`).
/// The small 2px corner is the inner-bottom tail in each case.
class ChatBubble extends StatelessWidget {
  const ChatBubble({required this.message, super.key});

  final ChatMessage message;

  static const Radius _r = Radius.circular(SimfTokens.radius); // 8 — large corners
  static const Radius _tail = Radius.circular(SimfTokens.radiusTail);

  @override
  Widget build(BuildContext context) {
    final isUser = message.author == ChatAuthor.user;
    final text = Text(
      message.text,
      style: TextStyle(
        color: isUser ? SimfTokens.surface : SimfTokens.chatBubbleText,
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
          horizontal: SimfTokens.chatBubblePadH,
          vertical: SimfTokens.space3,
        ),
        constraints: const BoxConstraints(
          maxWidth: SimfTokens.chatBubbleMaxWidth,
        ),
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
        vertical: SimfTokens.gap2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        AppL10n.of(context).aiBadgeLabel,
        style: SimfTokens.labelWhiteBold12Tall,
      ),
    );
  }
}
