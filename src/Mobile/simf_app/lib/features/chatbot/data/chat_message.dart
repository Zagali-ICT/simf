/// Who sent a chat line — drives bubble alignment and colour.
enum ChatAuthor { user, assistant }

/// One line in the transcript.
class ChatMessage {
  const ChatMessage(this.author, this.text);

  final ChatAuthor author;
  final String text;
}
