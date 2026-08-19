import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_search_field.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_repository.dart';
import 'package:simf_app/features/speakers/widgets/speaker_list_card.dart';
import 'package:simf_app/features/speakers/widgets/speaker_sort_control.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Speakers list — route: RouteNames.speakers · Figma 908:1744
class SpeakersScreen extends ConsumerStatefulWidget {
  const SpeakersScreen({super.key});

  @override
  ConsumerState<SpeakersScreen> createState() => _SpeakersScreenState();
}

class _SpeakersScreenState extends ConsumerState<SpeakersScreen> {
  // Frame 908:1744 — client-side search + alphabetical sort over the loaded
  // list. Default preserves the API's curated order; the sort control toggles
  // an A→Z alphabetical sort.
  String _query = '';
  bool _alphaSorted = false;

  Future<void> _refresh() => refreshAsync(ref, speakersListProvider.future);

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // DRY (owner 2026-06-28): the shared SimfPageShell standard nav now renders
    // the Figma sub-page header (forced-LTR back-left + centred title +
    // hairline), so the old per-screen header is gone.
    return SimfPageShell(
      title: l10n.speakersTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(speakersListProvider).when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
          // Both states are hosted in a scrollable so a pull still works and
          // the user can retry by pulling.
          error: (_, __) => SimfPullToRefresh(
            onRefresh: _refresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              children: <Widget>[
                SimfErrorState(
                  message: l10n.speakersError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(speakersListProvider),
                ),
              ],
            ),
          ),
          data: (speakers) => speakers.isEmpty
              ? SimfPullToRefresh(
                  onRefresh: _refresh,
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    children: <Widget>[
                      SimfEmptyState(
                        icon: Icons.groups_outlined,
                        message: l10n.speakersEmpty,
                      ),
                    ],
                  ),
                )
              : _buildDirectory(l10n, speakers),
        );
  }

  Widget _buildDirectory(AppL10n l10n, List<SpeakerSummary> speakers) {
    final isArabic = l10n.isArabic;
    // The card builds `{base}/app/assets/SpeakerPhoto/{id}/image` for the avatar.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final visible = _visibleSpeakers(speakers, isArabic);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Frame 908:1744 — the search box (start/right) + the sort control
        // (end/left). Width-flexible: the search Expands to fill the remaining
        // width on any screen (owner responsive + DRY 2026-06-28).
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space2,
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: SimfSearchField(
                  hint: l10n.speakersSearchHint,
                  onChanged: (v) => setState(() => _query = v),
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              SpeakerSortControl(
                label: l10n.speakersSortAlpha,
                selected: _alphaSorted,
                onTap: () => setState(() => _alphaSorted = !_alphaSorted),
              ),
            ],
          ),
        ),
        Expanded(
          child: SimfPullToRefresh(
            onRefresh: _refresh,
            child: visible.isEmpty
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    children: <Widget>[
                      SimfEmptyState(
                        icon: Icons.search_off_outlined,
                        message: l10n.speakersNoMatches,
                      ),
                    ],
                  )
                : ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(
                      SimfTokens.space4,
                      SimfTokens.space2,
                      SimfTokens.space4,
                      SimfTokens.space6,
                    ),
                    itemCount: visible.length,
                    // Frame 908:1744 — cards pitch 76px (card 60 + 16 gap).
                    separatorBuilder: (_, __) =>
                        const SizedBox(height: SimfTokens.space4),
                    itemBuilder: (context, index) {
                      final speaker = visible[index];
                      return SpeakerListCard(
                        speaker: speaker,
                        isArabic: isArabic,
                        baseUrl: baseUrl,
                        onTap: () => context.pushNamed(
                          RouteNames.speakerProfile,
                          pathParameters: <String, String>{
                            RouteParams.speakerId: speaker.id,
                          },
                        ),
                      );
                    },
                  ),
          ),
        ),
      ],
    );
  }

  /// The loaded speakers after the search query + alphabetical sort (908:1744).
  List<SpeakerSummary> _visibleSpeakers(
    List<SpeakerSummary> speakers,
    bool isArabic,
  ) =>
      visibleSpeakers(
        speakers,
        _query,
        isArabic: isArabic,
        alphaSorted: _alphaSorted,
      );
}
