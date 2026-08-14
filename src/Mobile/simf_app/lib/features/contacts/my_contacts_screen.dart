import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/contacts/widgets/contacts_empty_state.dart';
import 'package:simf_app/features/contacts/widgets/error_state.dart';
import 'package:simf_app/features/contacts/widgets/saved_contact_sheet.dart';
import 'package:simf_app/features/contacts/widgets/saved_contact_tile.dart';

/// My Contacts (SIMF-FDS-014 §5.6, D-286). **Auth-gated** (Approved only).
/// Lists the cards the visitor saved (`GET /app/contacts`, resolved on read —
/// no PII snapshot). A row opens a detail sheet to **export** the saved card as
/// a vCard (`GET /app/contacts/{id}/vcard`) or **remove** it (`DELETE
/// /app/contacts/{id}`, soft-delete). The app-bar scan action opens the scanner
/// to add more. UI is interim (final visuals from SIMF-VID-001).
///
/// Route: `RouteNames.myContacts`.
/// Data: [savedContactsProvider] over [contactsRepositoryProvider].
/// Perf: lazy — builds children on demand (ListView.builder).

/// The visitor's saved contact cards (`GET /app/contacts`).
///
/// No folding needed here, unlike the terms and news providers: "empty" is an
/// empty LIST, which `when`'s data branch already carries, so there is no extra
/// server outcome to map onto null.
final savedContactsProvider =
    FutureProvider.autoDispose<List<SavedContactRow>>(
  (ref) => ref.watch(contactsRepositoryProvider).listSaved(),
);

class MyContactsScreen extends ConsumerWidget {
  const MyContactsScreen({super.key});

  Future<void> _openScanner(BuildContext context, WidgetRef ref) async {
    await context.pushNamed(RouteNames.scanContact);
    // A save on the scanner closes it and returns here — reload to show it.
    ref.invalidate(savedContactsProvider);
  }

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
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.myContactsTitle),
        actions: <Widget>[
          IconButton(
            tooltip: l10n.contactScanAdd,
            onPressed: () => unawaited(_openScanner(context, ref)),
            icon: const Icon(Icons.qr_code_scanner),
          ),
        ],
      ),
      body: SafeArea(child: _buildBody(context, ref, l10n)),
    );
  }

  Widget _buildBody(BuildContext context, WidgetRef ref, AppL10n l10n) {
    Future<void> refresh() =>
        refreshAsync(ref, savedContactsProvider.future);

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
                    onAction: () => unawaited(_openScanner(context, ref)),
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
