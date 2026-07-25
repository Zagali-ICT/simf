import 'package:flutter/painting.dart';

/// Parses a stored `pageColor` string — the per-profile-type badge colour an
/// admin sets in the Control Panel, canonical form `#RRGGBB` — into a [Color].
///
/// The field is free text (≤ 32 chars), so this tolerates the shapes an admin
/// can actually type: an optional leading `#`, surrounding whitespace, the
/// `#RGB` shorthand the API docs use (`#0B5`), and an optional `AARRGGBB`
/// alpha. It returns `null` for anything it cannot parse so the caller falls
/// back to a design token; it never throws.
Color? parseHexColor(String? raw) {
  if (raw == null) {
    return null;
  }
  var hex = raw.trim();
  if (hex.startsWith('#')) {
    hex = hex.substring(1);
  }
  // `#RGB` shorthand → `RRGGBB` (each nibble doubled).
  if (hex.length == 3) {
    hex = hex.split('').map((c) => '$c$c').join();
  }
  // Assume opaque when no alpha byte is given (the canonical `#RRGGBB` case).
  if (hex.length == 6) {
    hex = 'FF$hex';
  }
  if (hex.length != 8) {
    return null;
  }
  final value = int.tryParse(hex, radix: 16);
  // Data-driven colour — not a hardcoded design literal (see CLAUDE.md §5.1).
  return value == null ? null : Color(value);
}
