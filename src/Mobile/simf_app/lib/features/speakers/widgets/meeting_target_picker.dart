import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/widgets/meeting_sheet_fields.dart';

/// One selectable row in the sheet's picker — a speaker (D-745: photo + name +
/// country + rank) or an invited delegation (flag + country + member count).
/// The sheet never learns which: it holds the [id] it submits against, the
/// search predicate the feature already owns on its own model, and a builder
/// for the feature's own tile widget.
@immutable
class MeetingTargetOption<T> {
  const MeetingTargetOption({
    required this.id,
    required this.value,
    required this.matches,
    required this.buildTile,
  });

  final String id;
  final T value;

  /// The feature's own type-to-filter predicate (`SpeakerSummary.matches` /
  /// `DelegationItem.matches`), so search behaves identically wherever a
  /// target is chosen.
  final bool Function(String query) matches;
  final Widget Function({required bool selected, required VoidCallback? onTap})
      buildTile;
}

/// The bilateral picker (owner 2026-07-11) — a searchable, selectable list of
/// every candidate target, shown only when the sheet was opened without a
/// fixed one. The list is height-capped and scrolls internally so a long
/// roster never pushes the subject/slots off-screen.
class MeetingTargetPicker<T> extends StatelessWidget {
  const MeetingTargetPicker({
    required this.loaded,
    required this.options,
    required this.selectedId,
    required this.query,
    required this.searchFieldKey,
    required this.searchHint,
    required this.emptyHint,
    required this.noMatchesHint,
    required this.pinSelected,
    required this.enabled,
    required this.onQueryChanged,
    required this.onSelected,
    super.key,
  });

  final bool loaded;
  final List<MeetingTargetOption<T>> options;
  final String? selectedId;
  final String query;
  final Key searchFieldKey;
  final String searchHint;

  /// Shown when the roster itself came back empty — a different fact from "the
  /// query matched nothing", which is [noMatchesHint].
  final String emptyHint;
  final String noMatchesHint;

  /// Keep the already-chosen row visible even when the query would filter it
  /// out, so the picker can never hide (or contradict) the target the form
  /// submits to. The speaker sheet does; the delegation sheet does not.
  final bool pinSelected;
  final bool enabled;
  final ValueChanged<String> onQueryChanged;
  final ValueChanged<MeetingTargetOption<T>> onSelected;

  @override
  Widget build(BuildContext context) {
    if (!loaded) {
      return const MeetingSheetSpinner();
    }
    if (options.isEmpty) {
      return MeetingFieldHint(text: emptyHint);
    }
    final matches = <MeetingTargetOption<T>>[
      for (final option in options)
        if ((pinSelected && option.id == selectedId) || option.matches(query))
          option,
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        MeetingSearchField(
          fieldKey: searchFieldKey,
          hintText: searchHint, // ما الذي تبحث عنه
          onChanged: onQueryChanged,
        ),
        const SizedBox(height: SimfTokens.space2),
        if (matches.isEmpty)
          MeetingFieldHint(text: noMatchesHint) // لا نتائج مطابقة
        else
          ConstrainedBox(
            constraints: const BoxConstraints(
              maxHeight: SimfTokens.meetingRequestSheetMaxHeight,
            ),
            child: ListView.separated(
              shrinkWrap: true,
              padding: EdgeInsets.zero,
              itemCount: matches.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space2),
              itemBuilder: (context, i) {
                final option = matches[i];
                return option.buildTile(
                  selected: option.id == selectedId,
                  onTap: enabled ? () => onSelected(option) : null,
                );
              },
            ),
          ),
      ],
    );
  }
}
