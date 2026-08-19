import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/widgets/about_forum_body.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// About the forum — عن الملتقى · route: RouteNames.aboutForum
/// Figma 1116:16448 · Contract: the vision paragraph hydrates from the CMS
///   (`GET /app/content/about`, D-173) and falls back to the bundled bilingual
///   copy; the mission line, details and themes are the forum's fixed framing.
class AboutScreen extends ConsumerStatefulWidget {
  const AboutScreen({super.key});

  @override
  ConsumerState<AboutScreen> createState() => _AboutScreenState();
}

class _AboutScreenState extends ConsumerState<AboutScreen> {
  ContentBlock? _block;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  /// Best-effort hydrate of the vision paragraph from the CMS. Any failure
  /// (incl. a 404 = key not seeded) leaves [_block] null and the screen renders
  /// the static fallback paragraph — the page always shows the forum content.
  Future<void> _load() async {
    try {
      final block =
          await ref.read(contentRepositoryProvider).getContentBlock('about');
      if (!mounted) {
        return;
      }
      setState(() => _block = block);
    } on ApiFailure {
      // Static fallback already covers this — nothing to surface.
    }
  }

  /// Owner rule: every data page pulls to refresh. The page renders the CMS
  /// block AND the edition config, so both are re-read. Each swallows its own
  /// failure (the static fallback covers it), so the gesture always completes.
  Future<void> _refresh() async {
    await Future.wait<void>(<Future<void>>[
      _load(),
      ref.read(orgProfileProvider.notifier).warm(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.aboutTitle,
      onBack: () => backOrHome(context),
      body: SimfPullToRefresh(
        onRefresh: _refresh,
        child: AboutForumBody(block: _block),
      ),
    );
  }
}
