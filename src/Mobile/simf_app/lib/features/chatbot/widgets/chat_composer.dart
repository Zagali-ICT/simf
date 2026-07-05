import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The bottom input bar (frame `1070:13398`): a navy-deep bar with the beige
/// hairline, the placeholder at the inline end and the gold send square at the
/// inline start (a spinner replaces the glyph while sending).
class ChatComposer extends StatelessWidget {
  const ChatComposer({
    required this.controller,
    required this.hint,
    required this.sendTooltip,
    required this.sending,
    required this.onSend,
    super.key,
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
