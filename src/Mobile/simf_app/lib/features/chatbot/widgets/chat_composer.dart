import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

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
      constraints: const BoxConstraints(minHeight: SimfTokens.controlHeight),
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
            // The placeholder disappears once the user types, so the message
            // box itself had no accessible name (BUG-012).
            child: Semantics(
              label: hint,
              textField: true,
              child: TextField(
                controller: controller,
                minLines: 1,
                maxLines: 4,
                textInputAction: TextInputAction.send,
                style: SimfTokens.bodyWhiteSm,
                onSubmitted: (_) => onSend(),
                decoration: InputDecoration(
                  hintText: hint,
                  hintStyle: SimfTokens.bodyWhiteSm,
                  isCollapsed: true,
                  filled: false,
                  border: InputBorder.none,
                  contentPadding: const EdgeInsets.symmetric(
                    vertical: SimfTokens.space2,
                  ),
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
                width: SimfTokens.sendSquareSize,
                height: SimfTokens.sendSquareSize,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
                child: sending
                    ? const SizedBox(
                        width: SimfTokens.space3,
                        height: SimfTokens.space3,
                        child: CircularProgressIndicator(
                          strokeWidth: SimfTokens.chatComposerStrokeWidth,
                          color: SimfTokens.surface,
                        ),
                      )
                    : const Icon(
                        Icons.send,
                        size: SimfTokens.chatComposerSize,
                        color: SimfTokens.surface,
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
