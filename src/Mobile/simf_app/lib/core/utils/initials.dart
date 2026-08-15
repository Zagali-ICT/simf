import 'package:characters/characters.dart';

/// Initials for an avatar or logo tile when there is no image.
///
/// There are **two** rules here, not one, and they render differently. Both
/// were duplicated across features, so both are shared — but they are kept
/// apart deliberately: collapsing them would silently change what a tile shows.

final RegExp _whitespace = RegExp(r'\s+');

/// The first one or two CHARACTERS of the name, uppercased. `''` when blank.
///
/// "Naval Command" gives "NA". Used by the entity-detail scaffold (exhibitor
/// and sponsor detail) and the booth officer row, which carried a
/// byte-identical private copy.
String initialsFromStart(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
}

/// The first LETTER of each of the first two words, uppercased. `'—'` when
/// there is nothing to take.
///
/// "Naval Command" gives "NC". Used by the sponsor badge and the media-partner
/// logo, which carried the same body under two names.
///
/// The em dash rather than `''` is deliberate: these tiles always draw
/// something, so an empty string would render an empty box.
String initialsFromWords(String name) {
  final words = name.trim().split(_whitespace);
  final letters = words
      .where((w) => w.isNotEmpty)
      .take(2)
      .map((w) => w.characters.first)
      .join();
  return letters.isEmpty ? '—' : letters.toUpperCase();
}
