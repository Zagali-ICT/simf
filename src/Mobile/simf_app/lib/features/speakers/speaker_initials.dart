/// First letters of the first two words of [name] — the interim avatar used by
/// the speakers list (Page_019) and profile (Page_020) until the photo asset
/// pass (SIMF-VID-001).
String speakerInitials(String name) {
  final words = name.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty);
  final letters = words.take(2).map((w) => w.substring(0, 1)).join();
  return letters.isEmpty ? '?' : letters.toUpperCase();
}
