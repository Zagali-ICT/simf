import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/faq/data/faq_repository.dart';
import 'package:simf_app/features/faq/widgets/faq_list.dart';

/// FAQ — route: `RouteNames.faq` · Figma 1388:7567
///
/// Data-driven from the public `GET /app/faq` (the D-211 FAQ tables).
class FaqScreen extends ConsumerWidget {
  const FaqScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final faq = ref.watch(faqProvider);

    Future<void> onRefresh() => refreshAsync(ref, faqProvider.future);

    return SimfPageShell(
      title: l10n.faqRowTitle,
      onBack: () => backOrHome(context),
      body: faq.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: onRefresh,
          child: SimfErrorState(
            message: l10n.faqError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(faqProvider),
          ),
        ),
        data: (groups) {
          final hasEntries = groups.any((g) => g.entries.isNotEmpty);
          if (!hasEntries) {
            return SimfRefreshableMessage(
              onRefresh: onRefresh,
              child: SimfEmptyState(
                icon: Icons.help_outline,
                message: l10n.faqEmpty,
              ),
            );
          }
          return FaqList(
            groups: groups,
            isArabic: isArabic,
            onRefresh: onRefresh,
          );
        },
      ),
    );
  }
}
