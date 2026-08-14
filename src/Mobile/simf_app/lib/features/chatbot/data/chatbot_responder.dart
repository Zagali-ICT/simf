import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/chatbot/data/chatbot_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The seam that turns a user prompt into an assistant reply.
///
/// Backed by the centralised AI (`POST /app/ai/assistance` — the `assistance`
/// prompt, grounded server-side on the live event context). Overridable via
/// [chatbotResponderProvider] so widget tests inject a fake responder (no
/// network).
abstract class ChatbotResponder {
  Future<String> reply(String prompt, {required bool isArabic});
}

/// Calls the centralised AI assistance endpoint and returns its answer text.
/// Throws [ApiFailure] on a wire error — the screen surfaces it as an error
/// bubble.
class ApiChatbotResponder implements ChatbotResponder {
  ApiChatbotResponder(this._client);

  final SimfApiClient _client;

  @override
  Future<String> reply(String prompt, {required bool isArabic}) {
    return _client.post<String>(
      ChatbotEndpoints.assistance,
      body: <String, dynamic>{
        'message': prompt,
        'locale': isArabic ? 'ar' : 'en',
      },
      decodeData: (data) {
        final map = data is Map ? data : const <dynamic, dynamic>{};
        return (map['outputText'] as String? ?? '').trim();
      },
    );
  }
}

/// Overridable seam so widget tests inject a fake responder (no network).
final chatbotResponderProvider = Provider<ChatbotResponder>(
  (ref) => ApiChatbotResponder(ref.watch(simfApiClientProvider)),
);
