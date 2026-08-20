import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/motion/motion_durations.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/organisation_typeahead_field.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// جهة العمل on the sign-up profile step, with its debounced search attached.
///
/// The search text, the debounce timer and the fetch state are private to this
/// field — nothing else on the screen reads them, and a keystroke here has no
/// business rebuilding the whole form. The screen keeps only what it submits:
/// the chosen organisation's id and the label shown once it is chosen.
///
/// D-375 — fetch state comes FIRST (a spinner while searching, a retry on
/// failure), so "no matches" only ever describes a COMPLETED empty search.
class SignUpVisitorOrganisationField extends ConsumerStatefulWidget {
  const SignUpVisitorOrganisationField({
    required this.l10n,
    required this.initialResults,
    required this.selectedId,
    required this.selectedLabel,
    required this.showError,
    required this.onSelected,
    required this.onCleared,
    super.key,
  });

  final AppL10n l10n;

  /// The organisations fetched with the rest of the screen's opening load, so
  /// the list is populated before the first keystroke.
  final List<OrganisationItem> initialResults;

  final String? selectedId;
  final String? selectedLabel;

  /// B3 (D-221) — organisation is required; flag an empty pick after a submit.
  final bool showError;

  final ValueChanged<OrganisationItem> onSelected;
  final VoidCallback onCleared;

  @override
  ConsumerState<SignUpVisitorOrganisationField> createState() =>
      _SignUpVisitorOrganisationFieldState();
}

class _SignUpVisitorOrganisationFieldState
    extends ConsumerState<SignUpVisitorOrganisationField> {
  final TextEditingController _search = TextEditingController();

  late List<OrganisationItem> _results;
  bool _searching = false;
  bool _searchFailed = false;
  Timer? _debounce;

  // Which search the UI is currently showing. The debounce cancels a
  // pending TIMER, but it cannot cancel a request already in flight, so
  // two searches can be outstanding at once and the network decides which
  // lands last. On the venue's congested WiFi that is routinely the OLDER
  // one: 'min' fires, the user keeps typing 'ministry', the short query's
  // slower response arrives afterwards and repaints the list with matches
  // for text the box no longer contains. Organisation is required (D-221),
  // so the visitor either picks the wrong employer or is told there is no
  // match for a query that has one.
  int _searchGeneration = 0;

  @override
  void initState() {
    super.initState();
    _results = widget.initialResults;
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(
      MotionDurations.searchDebounce,
      () => unawaited(_run(value)),
    );
  }

  Future<void> _run(String value) async {
    final generation = ++_searchGeneration;
    setState(() {
      _searching = true;
      _searchFailed = false;
    });
    final repo = ref.read(profileRepositoryProvider);
    try {
      final results = await repo.searchOrganisations(search: value);
      if (!mounted || generation != _searchGeneration) {
        return;
      }
      setState(() {
        _results = results;
        _searching = false;
      });
    } on ApiFailure {
      if (!mounted || generation != _searchGeneration) {
        return;
      }
      setState(() {
        _searching = false;
        _searchFailed = true;
      });
    }
  }

  void _select(OrganisationItem organisation) {
    setState(() => _results = const <OrganisationItem>[]);
    widget.onSelected(organisation);
  }

  void _clear() {
    _search.clear();
    widget.onCleared();
  }

  @override
  Widget build(BuildContext context) {
    return OrganisationTypeaheadField(
      l10n: widget.l10n,
      controller: _search,
      selectedId: widget.selectedId,
      selectedLabel: widget.selectedLabel,
      searching: _searching,
      searchFailed: _searchFailed,
      results: _results,
      showError: widget.showError,
      onSearchChanged: _onSearchChanged,
      onRetry: () => unawaited(_run(_search.text.trim())),
      onSelected: _select,
      onCleared: _clear,
    );
  }
}
