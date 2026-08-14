import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/widgets/programme_day_banner.dart';
import 'package:simf_app/features/sessions/widgets/programme_day_strip.dart';
import 'package:simf_app/features/sessions/widgets/session_timeline_row.dart';
import 'package:simf_app/features/sessions/widgets/session_type_tabs.dart';
import 'package:simf_app/features/sessions/widgets/sessions_search_field.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 016 — برنامج الملتقى · Sessions (#16, `/sessions`), rebuilt to the LIVE
/// KSA frame **883:2308** (D-452).
///
/// **Signed-in** (Visitor+) — D-576 login-gated: a guest who opens this route
/// is sent to sign-in (supersedes the earlier "public Guest+" access). The
/// screen fetches the day-grouped programme once
/// (`GET /app/programme/days`) and filters it client-side. Frame mapping: the
/// bordered search field; the **white day strip** (the programme days — the
/// selected day inverts to navy, weekend weekday labels render red); the
/// selected day's **day title + logo banner** ("تفاصيل اليوم" carries the day's
/// own title); the **type tabs** (الكل / جلسات / ورش العمل); then the
/// **المواعيد** list — the day's sessions filtered by the active type + the
/// search, the **first one featured** (expanded with the day banner image), the
/// rest collapsed. Tapping a row opens the session detail (Page_017). The
/// section widgets live in `widgets/` (sessions_search_field,
/// programme_day_strip, programme_day_banner, session_type_tabs,
/// session_timeline_row).
class SessionsScreen extends ConsumerStatefulWidget {
  const SessionsScreen({super.key});

  @override
  ConsumerState<SessionsScreen> createState() => _SessionsScreenState();
}

class _SessionsScreenState extends ConsumerState<SessionsScreen> {
  bool _loading = true;
  bool _error = false;
  List<ProgrammeDay> _days = const <ProgrammeDay>[];
  String? _selectedDayId;
  SessionType? _typeFilter; // null = الكل / All
  String _query = '';

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final days = await ref.read(sessionsRepositoryProvider).getDays();
      if (!mounted) {
        return;
      }
      setState(() {
        _days = days;
        // Open on the first day that actually has sessions, not blindly the
        // first day — otherwise a programme whose sessions sit on a later day
        // renders an empty schedule until the user taps that day by hand.
        _selectedDayId = days.isEmpty
            ? null
            : days
                .firstWhere(
                  (day) => day.sessions.isNotEmpty,
                  orElse: () => days.first,
                )
                .id;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  void _openSession(SessionListItem session) {
    unawaited(context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{RouteParams.sessionId: session.id},
      ));
  }

  /// The empty-list message for the active type tab — "no workshops" under
  /// ورش العمل, "no sessions" under جلسات, and a day-level "no programme" under
  /// الكل / All (where "no sessions" would misdescribe a day that simply has
  /// nothing scheduled). The tab-less event bucket shares the الكل message.
  String _filteredEmptyMessage(AppL10n l10n) => switch (_typeFilter) {
        SessionType.session => l10n.sessionsEmpty,
        SessionType.workshop => l10n.sessionsEmptyWorkshops,
        SessionType.event || null => l10n.sessionsEmptyDay,
      };

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      // Frame 883:2314 — the screen header is "برنامج الملتقى" (the bottom-nav
      // tab carries the shared "الجلسات" label, nav component 206:1732).
      title: l10n.sessionsProgrammeTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      showSweep: true,
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      // Pull-to-retry: the shared host keeps the pull gesture alive on the
      // short, centred error state.
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfErrorState(
          message: l10n.sessionsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    if (_days.isEmpty) {
      // Pull-to-refresh also works on the empty state.
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfEmptyState(
          icon: Icons.event_busy_outlined,
          message: l10n.sessionsEmpty,
        ),
      );
    }

    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final selected = _days.firstWhere(
      (d) => d.id == _selectedDayId,
      orElse: () => _days.first,
    );
    final dayImageUrl = selected.hasImage
        ? AssetUrls.image(baseUrl, AssetKind.programmeDayImage, selected.id)
        : null;

    // The selected day's sessions, filtered by the active type tab + the
    // search.
    final sessions = sessionsForDay(
      selected,
      type: _typeFilter,
      query: _query,
    );

    // The chrome is a fixed handful of widgets; the schedule is one row per
    // session for a server-driven day, so it builds lazily in its own sliver
    // rather than being spread eagerly into the same list (section 4).
    return SimfPullToRefresh(
      onRefresh: _load,
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: <Widget>[
          SliverPadding(
            // Top gap below the header matches Figma 883:2308 (title row →
            // search ≈ 32px; the shell header adds its own 8px, so the body
            // adds ~24px) — owner 2026-06-30: the header's bottom space was
            // too small.
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space6,
              SimfTokens.space4,
              0,
            ),
            sliver: SliverList(
              delegate: SliverChildListDelegate(<Widget>[
                SessionsSearchField(
                  l10n: l10n,
                  onChanged: (value) => setState(() => _query = value),
                ),
                const SizedBox(height: SimfTokens.space4),
                ProgrammeDayStrip(
                  days: _days,
                  selectedId: selected.id,
                  onChanged: (id) => setState(() => _selectedDayId = id),
                ),
                const SizedBox(height: SimfTokens.space5),
                // تفاصيل اليوم (883:2327 area) — the selected day's OWN title +
                // its logo banner. The "تفاصيل اليوم" label carries the day
                // title (owner: not a static label — it is the day's title).
                SimfSectionHeader(
                  title: selected.localizedTitle(isArabic: isArabic),
                ),
                const SizedBox(height: SimfTokens.space3),
                ProgrammeDayBanner(imageUrl: dayImageUrl),
                const SizedBox(height: SimfTokens.space5),
                // Type tabs (883:2320): الكل / جلسات / ورش العمل.
                SessionTypeTabs(
                  l10n: l10n,
                  active: _typeFilter,
                  onChanged: (type) => setState(() => _typeFilter = type),
                ),
                const SizedBox(height: SimfTokens.space5),
                SimfSectionHeader(title: l10n.sessionsScheduleSection),
                const SizedBox(height: SimfTokens.space3),
                if (sessions.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      vertical: SimfTokens.space4,
                    ),
                    child: Text(
                      _filteredEmptyMessage(l10n),
                      style: SimfTokens.hintBeige,
                    ),
                  ),
              ]),
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              0,
              SimfTokens.space4,
              SimfTokens.space6,
            ),
            sliver: SliverList.builder(
              itemCount: sessions.length,
              itemBuilder: (context, index) {
                final session = sessions[index];
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    if (index > 0) const SizedBox(height: SimfTokens.space4),
                    SessionTimelineRow(
                      session: session,
                      isArabic: isArabic,
                      // The first session of the day is featured — expanded
                      // with the day banner image (frame 1310:3232).
                      featuredImageUrl: index == 0 ? dayImageUrl : null,
                      onTap: () => _openSession(session),
                    ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
