import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/widgets/programme_body.dart';

/// Sessions — برنامج الملتقى · route: `RouteNames.sessions` · Figma 883:2308
class SessionsScreen extends ConsumerStatefulWidget {
  const SessionsScreen({super.key});

  @override
  ConsumerState<SessionsScreen> createState() => _SessionsScreenState();
}

class _SessionsScreenState extends ConsumerState<SessionsScreen> {
  String? _selectedDayId;
  SessionType? _typeFilter; // null = الكل / All
  String _query = '';

  Future<void> _refresh() => refreshAsync(ref, programmeDaysProvider.future);

  void _openSession(SessionListItem session) {
    unawaited(context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{RouteParams.sessionId: session.id},
      ),);
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
    return ref.watch(programmeDaysProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          // Pull-to-retry: the shared host keeps the pull gesture alive on the
          // short, centred error state.
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: _refresh,
            child: SimfErrorState(
              message: l10n.sessionsError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(programmeDaysProvider),
            ),
          ),
          data: (days) => days.isEmpty
              // Pull-to-refresh also works on the empty state.
              ? SimfRefreshableMessage(
                  onRefresh: _refresh,
                  child: SimfEmptyState(
                    icon: Icons.event_busy_outlined,
                    message: l10n.sessionsEmpty,
                  ),
                )
              : ProgrammeBody(
                  days: days,
                  l10n: l10n,
                  selectedDayId: _selectedDayId,
                  typeFilter: _typeFilter,
                  query: _query,
                  emptyMessage: _filteredEmptyMessage(l10n),
                  onQueryChanged: (value) => setState(() => _query = value),
                  onDayChanged: (id) => setState(() => _selectedDayId = id),
                  onTypeChanged: (type) => setState(() => _typeFilter = type),
                  onRefresh: _refresh,
                  onOpenSession: _openSession,
                ),
        );
  }
}
