import 'dart:typed_data';

import 'package:pointycastle/api.dart';
import 'package:pointycastle/block/aes.dart';
import 'package:pointycastle/block/modes/gcm.dart';

/// D-820 — decodes the encrypted badge a desk printed with no network.
///
/// The Dart twin of `SIMF.Common.Badges.EventBadgeCodec`. **Both sides must
/// agree byte for byte**: the plaintext is 20 RAW BYTES — a 16-byte profile id,
/// a 2-byte edition year and a 2-byte profile-type code, all big-endian —
/// AES-256-GCM encrypted with a 12-byte nonce and a full 16-byte tag, and the
/// wire form is one Crockford base32 key-version character followed by base32 of
/// `nonce || ciphertext || tag`.
///
/// Those three fields are the three questions a door has to answer with no
/// network: who is this, is the badge from the open year, and is this tier
/// admitted at THIS gate.
///
/// Decode-only on purpose. A scanner never mints a badge, and shipping an
/// encoder into the app would put a badge factory on every operator's phone.
class OfflineBadge {
  const OfflineBadge({
    required this.profileId,
    required this.editionYear,
    required this.profileTypeCode,
  });

  /// WHO — the attendee record, which every attendee has with or without an app
  /// account. Lower-case hyphenated form, matching what the server stores.
  final String profileId;

  /// WHEN — the edition the badge was issued for, so this device can refuse
  /// last year's badge without asking anyone.
  final int editionYear;

  /// WHAT — the badge type, as the small number the server calls
  /// `ProfileType.Code`. This is what makes the allowed-at-this-gate decision
  /// possible with no database.
  final int profileTypeCode;

  static const int _nonceBytes = 12;

  /// The FULL 16-byte tag. `pointycastle`'s GCM accepts nothing else, which is
  /// what forced the server codec off the 12-byte .NET minimum (D-820).
  static const int _tagBytes = 16;

  static const int _keyBytes = 32;

  /// A 16-byte profile id, a 2-byte edition year and a 2-byte type code.
  static const int _plaintextBytes = 20;

  /// Upper bound on an accepted scan, so a hostile or garbled input cannot push
  /// work into the decoder. A real badge is about 61 characters.
  static const int maxEncodedLength = 128;

  /// Length of an ordinary server-minted QR serial. A scanner cannot judge one
  /// offline — it has no roster — so callers abstain rather than decode.
  static const int mintedQrIdLength = 12;

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

  /// The plaintext is 20 RAW BYTES — a 16-byte profile id, a 2-byte edition
  /// year and a 2-byte profile-type code, all big-endian. There is no text
  /// decode any more: every byte is legitimately arbitrary, so the old
  /// ASCII check would have rejected most genuine badges.
  static OfflineBadge? _parsePayload(Uint8List plaintext) {
    if (plaintext.length != _plaintextBytes) {
      // A decrypt that succeeded but yielded another width is something else
      // encrypted under the same key, not a badge this system authored.
      return null;
    }

    final data = ByteData.sublistView(plaintext);
    return OfflineBadge(
      profileId: _formatGuid(plaintext.sublist(0, 16)),
      editionYear: data.getUint16(16),
      profileTypeCode: data.getUint16(18),
    );
  }

  /// The canonical 8-4-4-4-12 form, read straight off the wire bytes. The
  /// server writes the id big-endian for exactly this reason: .NET's own Guid
  /// layout is mixed-endian, and following it here would decode every badge to
  /// a different attendee.
  static String _formatGuid(List<int> bytes) {
    final hex = bytes
        .map((b) => b.toRadixString(16).padLeft(2, '0'))
        .join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-'
        '${hex.substring(12, 16)}-${hex.substring(16, 20)}-'
        '${hex.substring(20)}';
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
    // The encoder left-aligns its final group, so any leftover bits are its zero
    // padding and there are always fewer than five. Anything else means the
    // string was truncated or mangled — rejected rather than decoded into
    // plausible-looking bytes.
    if (bits >= 5 || buffer != 0) {
      return null;
    }
    return Uint8List.fromList(bytes);
  }
}
