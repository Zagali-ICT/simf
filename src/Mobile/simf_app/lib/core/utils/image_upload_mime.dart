/// The MIME type for an image the app is about to upload, or null when the
/// extension is not one the server's upload gate accepts.
///
/// The gate checks Content-Type against `image/jpeg | image/png | image/webp`
/// **and** the file's magic bytes, so a wrong or missing type makes the upload
/// 400. Every image upload in the app derives its type here.
///
/// Null means unsupported: the API client then omits the multipart content
/// type, leaving the server's magic-byte check as the authority. It must never
/// fall back to `image/jpeg` — that labels a file the app has not inspected.
String? imageUploadMime(String filename) {
  final lower = filename.toLowerCase();
  if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) {
    return 'image/jpeg';
  }
  if (lower.endsWith('.png')) {
    return 'image/png';
  }
  if (lower.endsWith('.webp')) {
    return 'image/webp';
  }
  return null;
}
