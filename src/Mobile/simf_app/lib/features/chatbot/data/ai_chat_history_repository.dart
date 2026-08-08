import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/chatbot/data/chat_message.dart';
import 'package:simf_app/features/chatbot/data/chatbot_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Loads the signed-in visitor's persisted AI-assistant transcript
/// (`GET /app/ai/assistance/history`) so the conversation survives navigation
/// and app restarts — the assistant also remembers it server-side. Each turn is
/// `{ role: "user" | "assistant", content }`. Throws [ApiFailure] on a wire
/// error; the screen falls back to the greeting-only view.
class AiChatHistoryRepository {
  AiChatHistoryRepository(this._client);

  final SimfApiClient _client;

  Future<List<ChatMessage>> getHistory() {
    return _client.get<List<ChatMessage>>(
      ChatbotEndpoints.history,
      decodeData: (data) => ((data as List?) ?? const <dynamic>[])
          .map((e) => (e as Map).cast<String, dynamic>())
          .map(
            (m) => ChatMessage(
              (m['role'] as String? ?? '').toLowerCase() == 'user'
                  ? ChatAuthor.user
                  : ChatAuthor.assistant,
              (m['content'] as String? ?? '').trim(),
            ),
          )
          .where((m) => m.text.isNotEmpty)
          .toList(),
    );
  }
}

final aiChatHistoryRepositoryProvider = Provider<AiChatHistoryRepository>(
  (ref) => AiChatHistoryRepository(ref.watch(simfApiClientProvider)),
);

/// The visitor's saved transcript, loaded when the chatbot opens. Auto-disposes
/// so the next visit refetches (picking up turns added since).
final aiChatHistoryProvider = FutureProvider.autoDispose<List<ChatMessage>>(
  (ref) => ref.watch(aiChatHistoryRepositoryProvider).getHistory(),
);
