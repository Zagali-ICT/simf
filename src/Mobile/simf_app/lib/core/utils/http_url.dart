/// True when [value] is an absolute http(s) URL the app can load directly
/// (guards `Image.network`). A media url / photo path holds either such a URL
/// (rendered) or a legacy relative path (falls back to a glyph / initials), so
/// the app never tries to load an unservable path.
bool isHttpUrl(String? value) {
  final v = value?.trim().toLowerCase() ?? '';
  return v.startsWith('http://') || v.startsWith('https://');
}
