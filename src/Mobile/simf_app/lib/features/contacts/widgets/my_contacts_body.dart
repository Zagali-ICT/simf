import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_providers.dart';
import 'package:simf_app/features/contacts/widgets/contacts_empty_state.dart';
import 'package:simf_app/features/contacts/widgets/error_state.dart';
import 'package:simf_app/features/contacts/widgets/saved_contact_sheet.dart';
import 'package:simf_app/features/contacts/widgets/saved_contact_tile.dart';

/// The My-Contacts list body: loading / error / empty / rows, each branch
/// refreshable. A row opens the detail sheet (export vCard, remove).
class MyContactsBody extends ConsumerWidget {
  const MyContactsBody({required this.onScan, super.key});

  /// Opens the scanner — shared with the app-bar action so both add-paths are
  /// the same call.
  final VoidCallback onScan;

  Future<void> _openDetail(
    BuildContext context,
    WidgetRef ref,
    SavedContactRow row,
  ) async {
    // Both captured BEFORE the sheet's async gap - reading them after it is
    // what `use_build_context_synchronously` is for.
    final messenger = ScaffoldMessenger.of(context);
    final removedText = AppL10n.of(context).myContactsRemoved;
    final removed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => SavedContactSheet(row: row),
    );
    if (removed ?? false) {
      messenger.showSnackBar(SnackBar(content: Text(removedText)));
      ref.invalidate(savedContactsProvider);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    Future<void> refresh() => refreshAsync(ref, savedContactsProvider.future);

    return ref.watch(savedContactsProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: refresh,
            child: ErrorState(
              message: l10n.myContactsError,
              onRetry: () => ref.invalidate(savedContactsProvider),
            ),
          ),
          data: (rows) => rows.isEmpty
              ? SimfRefreshableMessage(
                  onRefresh: refresh,
                  child: ContactsEmptyState(
                    title: l10n.myContactsEmpty,
                    hint: l10n.myContactsEmptyHint,
                    actionLabel: l10n.contactScanAdd,
                    onAction: onScan,
                  ),
                )
              : SimfPullToRefresh(
                  onRefresh: refresh,
                  child: ListView.builder(
                    padding: const EdgeInsets.all(SimfTokens.space4),
                    itemCount: rows.length,
                    itemBuilder: (context, index) {
                      final row = rows[index];
                      return SavedContactTile(
                        row: row,
                        isArabic: l10n.isArabic,
                        onTap: () => unawaited(_openDetail(context, ref, row)),
                      );
                    },
                  ),
                ),
        );
  }
}
