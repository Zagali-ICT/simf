using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for <c>CorrelationIdMiddleware</c>.</summary>
public sealed class CorrelationIdTests : IClassFixture<SimfApiFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdTests(SimfApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_response_carries_a_correlation_id_when_none_was_sent()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(
            response.Headers.GetValues("X-Correlation-Id").Single()));
    }

    [Fact]
    public async Task A_well_formed_inbound_correlation_id_is_echoed_back()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "trace-abc-123");

        var response = await _client.SendAsync(request);

        Assert.Equal(
            "trace-abc-123",
            response.Headers.GetValues("X-Correlation-Id").Single());
    }
}
