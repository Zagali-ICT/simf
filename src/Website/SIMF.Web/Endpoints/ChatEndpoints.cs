using SIMF.ApiClient;

namespace SIMF.Web.Endpoints;

// Same-origin BFF proxy for the public floating chat assistant. The API has no
// CORS policy, so the browser cannot call /app/ai/faq directly; the widget
// (initChatbot in landing.js) posts a question here and this endpoint server-side
// asks the anonymous FAQ assistant (grounded on the forum FAQ by the centralised
// AI), returning just the answer text. Mirrors the SiteContentEndpoints /
// AccountEndpoints proxy pattern.
internal static class ChatEndpoints
{
    private const int MaxQuestionLength = 1000;

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chat/ask", async (ChatAskRequest body, SimfPublicClient api, CancellationToken ct) =>
        {
            var question = (body?.Question ?? string.Empty).Trim();
            if (question.Length == 0)
            {
                return Results.BadRequest(new { error = "empty" });
            }
            if (question.Length > MaxQuestionLength)
            {
                question = question[..MaxQuestionLength];
            }

            var result = await api.AskFaqAsync(question, ct);
            var answer = result?.OutputText;
            return string.IsNullOrWhiteSpace(answer)
                ? Results.Json(new ChatAskResponse(null), statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Json(new ChatAskResponse(answer));
        });
    }

    private sealed class ChatAskRequest
    {
        public string? Question { get; set; }
    }

    private sealed record ChatAskResponse(string? Answer);
}
