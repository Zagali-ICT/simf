import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/exhibitor/widgets/my_visitors_body.dart';

/// My Booth Visitors — زوار جناحي · route: RouteNames.myVisitors
/// Contract: D-426 — the BOOTH's captured leads, newest first, each card
///   resolved live. Approved + non-visitor only (a visitor-tier caller gets a
///   403 and its own copy). BUG-025: this is NOT "My Contacts" (`/contacts`,
///   visitor-to-visitor sharing) — the two lists stay separate pending an owner
///   ruling, so a SimfPageNote states the difference in both languages.
class MyVisitorsScreen extends ConsumerWidget {
  const MyVisitorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.myVisitorsTitle,
      onBack: () => backOrHome(context),
      body: const MyVisitorsBody(),
    );
  }
}
