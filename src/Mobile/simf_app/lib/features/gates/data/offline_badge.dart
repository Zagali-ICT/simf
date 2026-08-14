import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/api.dart';
import 'package:pointycastle/block/aes.dart';
import 'package:pointycastle/block/modes/gcm.dart';

/// D-820 — decodes the encrypted badge a desk printed with no network.
///
/// The Dart twin of `SIMF.Common.Badges.EventBadgeCodec` + `OfflineBadgeId`.
/// **Both sides must agree byte for byte**: the plaintext is the ASCII string
/// `"{profileTypeCode},{sequence}"`, AES-256-GCM encrypted with a 12-byte nonce
/// and a full 16-byte tag, and the wire form is one Crockford base32
/// key-version character followed by base32 of `nonce || ciphertext || tag`.
///
/// Decode-only on purpose. A scanner never mints a badge, and shipping an
/// encoder into the app would put a badge factory on every operator's phone.
class OfflineBadge {
  const OfflineBadge({required this.profileTypeCode, required this.sequence});

  /// The badge type, as the small number the server calls `ProfileType.Code`.
  /// This is what makes the allowed-at-this-gate decision possible with no
  /// database.
  final int profileTypeCode;

  /// The desk's sequence — the badge's identity.
  final int sequence;

  /// The 12-character QR id this sequence maps to on the server, so an
  /// operator can read out something the Control Panel can find.
  String get qrId => sequence >= _minSequence && sequence <= _maxSequence
      ? 'W${sequence.toString().padLeft(11, '0')}'
      : '';

  static const int _nonceBytes = 12;

  /// The FULL 16-byte tag. `pointycastle`'s GCM accepts nothing else, which is
  /// what forced the server codec off the 12-byte .NET minimum (D-820).
  static const int _tagBytes = 16;

  static const int _keyBytes = 32;

  /// Upper bound on an accepted scan, so a hostile or garbled input cannot push
  /// work into the decoder. A real badge is about 61 characters.
  static const int maxEncodedLength = 128;

  /// Length of an ordinary server-minted QR serial. A scanner cannot judge one
  /// offline — it has no roster — so callers abstain rather than decode.
  static const int mintedQrIdLength = 12;

  static const int _minSequence = 1;

  /// One below 10^10 — the cap that keeps a leading zero in the padded id, so
  /// an offline id can never collide with a server-minted one.
  static const int _maxSequence = 9999999999;

  /// Reads the key version without decrypting, so a caller can pick between the
  /// current and the previous key during a rotation window.
  static int? readKeyVersion(String encoded) {
    if (encoded.isEmpty || encoded.length > maxEncodedLength) {
      return null;
    }
    return _CrockfordBase32.decodeSymbol(encoded[0]);
  }

  /// Decrypts a badge. Returns null for anything this key cannot open — a
  /// foreign key, a tampered payload, or a code that is not a badge at all.
  /// One null for every failure on purpose: a scanner that distinguished them
  /// would tell an attacker which of their guesses was closer.
  static OfflineBadge? decode(String encoded, Uint8List key) {
    if (encoded.isEmpty ||
        encoded.length > maxEncodedLength ||
        key.length != _keyBytes) {
      return null;
    }

    final body = _CrockfordBase32.decode(encoded.substring(1));
    if (body == null || body.length <= _nonceBytes + _tagBytes) {
      return null;
    }

    final nonce = Uint8List.sublistView(body, 0, _nonceBytes);
    final sealed = Uint8List.sublistView(body, _nonceBytes);

    final Uint8List plaintext;
    try {
      final cipher = GCMBlockCipher(AESEngine())
        ..init(
          false,
          AEADParameters(
            KeyParameter(key),
            _tagBytes * 8,
            nonce,
            Uint8List(0),
          ),
        );
      plaintext = cipher.process(sealed);
    } on InvalidCipherTextException {
      // The authentication tag did not verify: wrong key or altered bytes.
      return null;
    } on ArgumentError {
      return null;
    }

    return _parsePayload(plaintext);
  }

  static OfflineBadge? _parsePayload(Uint8List plaintext) {
    final String text;
    try {
      text = ascii.decode(plaintext);
    } on FormatException {
      return null;
    }

    final comma = text.indexOf(',');
    if (comma <= 0 || comma == text.length - 1) {
      return null;
    }
    final code = int.tryParse(text.substring(0, comma));
    final sequence = int.tryParse(text.substring(comma + 1));
    if (code == null || sequence == null || code < 0 || sequence < 0) {
      return null;
    }
    return OfflineBadge(profileTypeCode: code, sequence: sequence);
  }
}

/// Crockford base32, matching `SIMF.Common.Badges.CrockfordBase32`.
class _CrockfordBase32 {
  static const String _alphabet = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

  /// Decodes one 5-bit symbol, folding the look-alike characters Crockford
  /// defines: I and L read as 1, O reads as 0. That folding is why a damaged
  /// badge can be typed in by hand at the desk.
  static int? decodeSymbol(String symbol) {
    if (symbol.isEmpty) {
      return null;
    }
    final upper = symbol.toUpperCase();
    switch (upper) {
      case 'I':
      case 'L':
        return 1;
      case 'O':
        return 0;
    }
    final index = _alphabet.indexOf(upper);
    return index < 0 ? null : index;
  }

  static Uint8List? decode(String value) {
    if (value.isEmpty) {
      return null;
    }
    var buffer = 0;
    var bits = 0;
    final bytes = <int>[];
    for (var i = 0; i < value.length; i++) {
      final symbol = decodeSymbol(value[i]);
      if (symbol == null) {
        return null;
      }
      buffer = (buffer << 5) | symbol;
      bits += 5;
      if (bits >= 8) {
        bits -= 8;
        bytes.add((buffer >> bits) & 0xFF);
      }
      // Drop the bits already emitted. The C# side gets this free from int
      // overflow; keeping the buffer trimmed here makes the two implementations
      // agree by construction rather than by luck about integer width.
      buffer &= (1 << bits) - 1;
    }
    // The encoder left-aligns its final group, so any leftover bits are its
    // zero padding and there are always fewer than five. Anything else means
    // the string was truncated or mangled — rejected rather than decoded into
    // plausible-looking bytes.
    if (bits >= 5 || buffer != 0) {
      return null;
    }
    return Uint8List.fromList(bytes);
  }
}
