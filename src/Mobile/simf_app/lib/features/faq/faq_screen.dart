import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/faq/data/faq_repository.dart';
import 'package:simf_app/features/faq/widgets/faq_tile.dart';

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
    Future<void> onRefresh() => refreshAsync(ref, faqProvider.future);

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
                    SimfSectionHeader(title: group.localizedName(isArabic: isArabic)),
                    const SizedBox(height: SimfTokens.space3),
                  ],
                  for (final entry in group.entries) ...<Widget>[
                    FaqTile(entry: entry, isArabic: isArabic),
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
