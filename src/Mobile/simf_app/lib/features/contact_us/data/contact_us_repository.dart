import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/contact_us/data/contact_us_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the contact form (`POST /app/contact-inquiry`, anonymous —
/// Figma 1388:7567). One write; throws [ApiFailure] on a wire error (the screen
/// maps it to the send-failed toast). The server captures the signed-in
/// submitter from the bearer token when one is present.
class ContactUsRepository {
  ContactUsRepository(this._client);

  final SimfApiClient _client;

  Future<void> submit({
    required String name,
    required String email,
    required String message,
  }) {
    return _client.post<bool>(
      ContactUsEndpoints.inquiry,
      body: <String, dynamic>{
        'name': name,
        'email': email,
        'message': message,
      },
      decodeData: (_) => true,
    );
  }
}

final contactUsRepositoryProvider = Provider<ContactUsRepository>((ref) {
  return ContactUsRepository(ref.watch(simfApiClientProvider));
});
