import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// One option for the shared [LookupSearchSheet]: a stable [value], a display
/// [label], and the [search] text matched against the query (defaults to the
/// label).
class PickerOption {
  const PickerOption({
    required this.value,
    required this.label,
    String? search,
  }) : search = search ?? label;

  final String value;
  final String label;
  final String search;
}

/// D-373/D-469/D-470 — the shared searchable picker sheet used by the
/// nationality, birth-region and plate-letter fields: one beige type-to-filter
/// list so all three look and behave identically. Pops the picked
/// [PickerOption.value].
class LookupSearchSheet extends StatefulWidget {
  const LookupSearchSheet({
    required this.options,
    required this.searchHint,
    this.searchFieldKey,
    super.key,
  });

  final List<PickerOption> options;
  final String searchHint;
  final Key? searchFieldKey;

  @override
  State<LookupSearchSheet> createState() => _LookupSearchSheetState();
}

class _LookupSearchSheetState extends State<LookupSearchSheet> {
  static const TextStyle _itemStyle = TextStyle(
    fontSize: SimfTokens.textMd,
    fontWeight: FontWeight.w500,
    color: SimfTokens.inputInk,
  );

  String _query = '';

  @override
  Widget build(BuildContext context) {
    final term = _query.trim().toLowerCase();
    final filtered = term.isEmpty
        ? widget.options
        : widget.options
            .where((o) => o.search.toLowerCase().contains(term))
            .toList();
    return SafeArea(
      child: Padding(
        // Keeps the search field above the soft keyboard.
        padding: EdgeInsets.only(
          bottom: MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SizedBox(
          height: MediaQuery.sizeOf(context).height * 0.7,
          child: Column(
            children: <Widget>[
              Padding(
                padding: const EdgeInsets.fromLTRB(SimfTokens.space4,
                    SimfTokens.space4, SimfTokens.space4, SimfTokens.space2,),
                child: TextField(
                  key: widget.searchFieldKey,
                  autofocus: true,
                  style: _itemStyle,
                  onChanged: (value) => setState(() => _query = value),
                  decoration: InputDecoration(
                    isDense: true,
                    hintText: widget.searchHint,
                    hintStyle: const TextStyle(color: SimfTokens.greyText),
                    prefixIcon:
                        const Icon(Icons.search, color: SimfTokens.greyText),
                    // Radius uses OutlineInputBorder's default (circular 4 ==
                    // SimfTokens.radiusSmall); passing it trips
                    // avoid_redundant_argument_values.
                    enabledBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.beigeBorder),
                    ),
                    focusedBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.accent),
                    ),
                  ),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: filtered.length,
                  itemBuilder: (context, index) {
                    final option = filtered[index];
                    return ListTile(
                      dense: true,
                      title: Text(option.label, style: _itemStyle),
                      onTap: () => Navigator.of(context).pop(option.value),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Opens the shared searchable picker sheet and returns the picked
/// [PickerOption.value], or null when the sheet is dismissed.
///
/// Every lookup field presents the same beige type-to-filter sheet, and each
/// used to hand-roll this identical `showModalBottomSheet` call. One launcher
/// keeps them visually identical — the shape, the beige fill and the scroll
/// behaviour are decided here rather than at each call site.
Future<String?> showLookupSearchSheet({
  required BuildContext context,
  required List<PickerOption> options,
  required String searchHint,
  Key? searchFieldKey,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    backgroundColor: SimfTokens.cardBeige,
    shape: const RoundedRectangleBorder(
      borderRadius:
          BorderRadius.vertical(top: Radius.circular(SimfTokens.radiusLarge)),
    ),
    builder: (_) => LookupSearchSheet(
      options: options,
      searchHint: searchHint,
      searchFieldKey: searchFieldKey,
    ),
  );
}
