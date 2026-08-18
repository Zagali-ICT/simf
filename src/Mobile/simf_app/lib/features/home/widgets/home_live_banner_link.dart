import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/home/widgets/home_banners.dart';
import 'package:simf_app/features/live/data/current_live_session.dart';

/// The live banner (frame node 758:1150) — deep-links to the session that is
/// live right now (resolved on tap from the shared programme cache) so it opens
/// that session's feed, not the empty "no live session" screen. With nothing
/// live it opens id-less and falls back to the live screen's event-wide stream.
class HomeLiveBannerLink extends ConsumerWidget {
  const HomeLiveBannerLink({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return LiveBanner(
      l10n: l10n,
      onTap: () async {
        final liveId = await ref.read(currentLiveSessionIdProvider.future);
        if (!context.mounted) {
          return;
        }
        unawaited(
          context.pushNamed(
            RouteNames.liveBroadcast,
            queryParameters: liveId != null
                ? <String, String>{RouteParams.sessionId: liveId}
                : const <String, String>{},
          ),
        );
      },
    );
  }
}
