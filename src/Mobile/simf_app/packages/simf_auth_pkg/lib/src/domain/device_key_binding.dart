import 'dart:convert';

import 'package:meta/meta.dart';
import 'package:pointycastle/digests/sha256.dart';

/// Which account the biometric device key on THIS install belongs to.
///
/// The device key is a discoverable credential: the wire request carries only
/// `{deviceKeyId, challenge, signature}` and the server resolves the account
/// from the key row, so the email typed on the sign-in screen has never had any
/// bearing on which session comes back. Without a record of the owner the
/// sign-in screen could therefore offer an anonymous "Face ID" button that
/// signed the holder into whichever account last enrolled, whatever the form
/// said. This binding is that record.
///
/// It is written at enrolment, read before the challenge, and cleared whenever
/// the key is cleared. A key with no binding is treated as not enrolled.
@immutable
class DeviceKeyBinding {
  const DeviceKeyBinding({
    required this.userId,
    required this.emailDigest,
    required this.maskedEmail,
  });

  factory DeviceKeyBinding.fromJson(Map<String, dynamic> json) =>
      DeviceKeyBinding(
        userId: json['userId'] as String? ?? '',
        emailDigest: json['emailDigest'] as String? ?? '',
        maskedEmail: json['maskedEmail'] as String? ?? '',
      );

  /// Builds the binding for [email] enrolling under [deviceKeyId].
  factory DeviceKeyBinding.create({
    required String userId,
    required String deviceKeyId,
    required String email,
  }) =>
      DeviceKeyBinding(
        userId: userId,
        emailDigest: digestFor(deviceKeyId: deviceKeyId, email: email),
        maskedEmail: mask(email),
      );

  /// The owning account's id. Used to reconcile the binding against a session
  /// that arrives by some other route, never to match the typed email - an
  /// account can be re-addressed server-side, and the id cannot.
  final String userId;

  /// A digest of the owning address, NOT the address itself.
  ///
  /// This value is read on the sign-in screen, before anything has been
  /// authenticated, by whoever is holding the phone. Storing the plain address
  /// there would hand it to them. Salting with the server-issued key id
  /// costs nothing - it is already stored beside this - and stops one person's
  /// address producing the same digest on two different installs.
  ///
  /// It is not a secret-keeping measure: anyone who can read this can read the
  /// private key next to it and has already won. It keeps a full address off a
  /// pre-auth surface, which is a smaller and achievable claim.
  final String emailDigest;

  /// The address as shown on the button, e.g. `a***@example.sa`.
  final String maskedEmail;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'userId': userId,
        'emailDigest': emailDigest,
        'maskedEmail': maskedEmail,
      };

  /// Whether [email] is the address this key was enrolled for. Trimmed and
  /// lower-cased on both sides, so a reader's capitalisation never locks them
  /// out of their own credential.
  bool matchesEmail({required String deviceKeyId, required String email}) =>
      emailDigest.isNotEmpty &&
      emailDigest == digestFor(deviceKeyId: deviceKeyId, email: email);

  /// `sha256(deviceKeyId + ':' + normalisedEmail)`, hex.
  static String digestFor({
    required String deviceKeyId,
    required String email,
  }) {
    final normalised = email.trim().toLowerCase();
    final bytes = utf8.encode('$deviceKeyId:$normalised');
    final digest = SHA256Digest().process(bytes);
    final buffer = StringBuffer();
    for (final byte in digest) {
      buffer.write(byte.toRadixString(16).padLeft(2, '0'));
    }
    return buffer.toString();
  }

  /// Mirrors the server's `EmailMask`: the first character of the local part,
  /// three stars, then the full domain. The step-up screen shows a
  /// server-masked address, and the two must look identical or the same person
  /// appears to be two different accounts.
  static String mask(String email) {
    final trimmed = email.trim();
    final at = trimmed.indexOf('@');
    if (at <= 0) {
      return '***';
    }
    return '${trimmed[0]}***${trimmed.substring(at)}';
  }
}

/// This install's biometric credential: the server-issued key id paired with
/// the account it opens. The id is the salt of [DeviceKeyBinding.emailDigest],
/// so anything asking "does the typed address match?" needs both.
typedef EnrolledDeviceKey = ({String id, DeviceKeyBinding binding});

/// What `AuthController.signInWithDeviceKey` did.
///
/// An outcome rather than a thrown `AuthFailure`: every failure subtype needs
/// an `ApiFailure` source, and fabricating a server envelope for a refusal that
/// never left the handset would be a lie the error-mapping layer then has to
/// unpick.
enum DeviceKeySignInOutcome {
  /// A session was established for the key's owner.
  signedIn,

  /// No usable key on this install (never enrolled, revoked, or an upgraded
  /// install whose key predates the owner binding).
  notEnrolled,

  /// A key is enrolled, but for a different account than the one asked for.
  /// Refused locally, before the challenge - no network call, and no OS prompt
  /// spent on a sign-in that could not have succeeded.
  accountMismatch,
}
