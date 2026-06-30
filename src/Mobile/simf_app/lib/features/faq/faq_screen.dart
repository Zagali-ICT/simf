import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/faq_models.dart';
import 'data/faq_repository.dart';

/// Page 201 — الأسئلة الشائعة · FAQ (`/faq`, public). Pixel-parity to KSA Figma
/// frame **1388:7567**: the navy [SimfPageShell] shell over an accordion of
/// question/answer cards (tap a question to expand its answer). Data-driven from
/// the public `GET /app/faq` (the D-211 FAQ tables); previously a ComingSoon
/// placeholder (D-464).
///
/// Group names are surfaced as section headers only when there is more than one
/// group — a single-group catalogue renders the flat accordion the design shows.
class FaqScreen extends ConsumerWidget {
  const FaqScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final faq = ref.watch(faqProvider);

    // Pull-to-refresh — re-fetch the FAQ catalogue (invalidate + await next).
    Future<void> onRefresh() async {
      ref.invalidate(faqProvider);
      await ref.read(faqProvider.future);
    }

    return SimfPageShell(
      title: l10n.faqRowTitle,
      onBack: () => backOrHome(context),
      body: faq.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: SimfPullableHost(
            child: SimfErrorState(
              message: l10n.faqError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(faqProvider),
            ),
          ),
        ),
        data: (groups) {
          final hasEntries = groups.any((g) => g.entries.isNotEmpty);
          if (!hasEntries) {
            return SimfPullToRefresh(
              onRefresh: onRefresh,
              child: SimfPullableHost(
                child: SimfEmptyState(
                  icon: Icons.help_outline,
                  message: l10n.faqEmpty,
                ),
              ),
            );
          }
          final showGroupHeaders = groups.length > 1;
          return SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                SimfTokens.space4,
                SimfTokens.space4,
                SimfTokens.space4,
                SimfTokens.space6,
              ),
              children: <Widget>[
              for (final group in groups)
                if (group.entries.isNotEmpty) ...<Widget>[
                  if (showGroupHeaders) ...<Widget>[
                    SimfSectionHeader(title: group.localizedName(isArabic)),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                  for (final entry in group.entries) ...<Widget>[
                    _FaqTile(entry: entry, isArabic: isArabic),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

/// One accordion card (frame 1388:7569…): the question row with a gold
/// expand/collapse chevron, revealing the answer below a hairline when open.
class _FaqTile extends StatefulWidget {
  const _FaqTile({required this.entry, required this.isArabic});

  final FaqEntry entry;
  final bool isArabic;

  @override
  State<_FaqTile> createState() => _FaqTileState();
}

class _FaqTileState extends State<_FaqTile> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final question = widget.entry.localizedQuestion(widget.isArabic);
    final answer = widget.entry.localizedAnswer(widget.isArabic);
    return SimfCard(
      onTap: () => setState(() => _expanded = !_expanded),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7577)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    question,
                    textAlign: TextAlign.start,
                    // beige Medium 14 — Figma 1388:7582
                    style: SimfTokens.labelBeigeMedium,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Icon(
                  _expanded
                      ? Icons.keyboard_arrow_up_rounded
                      : Icons.keyboard_arrow_down_rounded,
                  color: SimfTokens.accent,
                  size: 20,
                ),
              ],
            ),
            if (_expanded) ...<Widget>[
              const SizedBox(height: SimfTokens.space2),
              const Divider(
                height: 1,
                thickness: SimfTokens.hairline,
                color: SimfTokens.beigeBorder,
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                answer,
                textAlign: TextAlign.start,
                style: SimfTokens.bodyBeige, // beige 14, line-height 1.5
              ),
            ],
          ],
        ),
      ),
    );
  }
}
