// What the desk does with the answers a venue's network actually gives it.
//
// These pin two crashes rather than two features. The client used to read the
// body before the status, so the two replies a badge desk meets most — a 401 with
// no body when the pasted token has expired, and a captive portal's HTML login
// page returned with a 200 — both escaped as exceptions no caller catches. That
// was survivable while uploading was a button somebody pressed. It is not
// survivable on a timer: the loop would fault a task nobody awaits and the desk
// would go on ticking, uploading nothing, looking perfectly healthy.
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SIMF.Common;
using SIMF.Contracts.Badges;
using Xunit;

namespace SIMF.BadgeDesk.Tests;

public sealed class UploadClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> reply)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(reply(request));
        }
    }

    private static List<OfflineBadgeRegistration> OneRow() =>
    [
        new()
        {
            ProfileId = Guid.NewGuid(),
            Sequence = 3_000_001,
            ProfileTypeCode = 1,
            Name = "Test Visitor",
            NationalId = "1098765432",
        },
    ];

    /// <summary>One upload against a server that always answers
    /// <paramref name="reply"/>, ready to be asserted on.</summary>
    private static Func<Task> Uploading(HttpResponseMessage reply)
    {
        var client = new UploadClient(new HttpClient(new StubHandler(_ => reply)));
        return () => client.UploadAsync(
            "https://desk.test", "shift-token", "Desk 3", OneRow());
    }

    [Fact]
    public async Task An_expired_token_is_reported_as_a_credential_failure_not_a_crash()
    {
        // The API answers 401 with NO BODY. Reading that as JSON throws, and the
        // failure that most needed reporting was the one that took the desk down.
        var upload = Uploading(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var failure = await upload.Should().ThrowAsync<UploadFailedException>();

        failure.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        failure.Which.IsCredentialFailure.Should().BeTrue(
            "the background loop discards the token on this answer instead of "
            + "replaying a rejected credential for three days");
        failure.Which.Message.Should().Contain("401");
    }

    [Fact]
    public async Task A_captive_portal_answering_200_with_a_login_page_is_just_a_retry()
    {
        // This is the network a badge desk sits on, and it is the reason the desk
        // does not probe connectivity: a portal answers everything, cheerfully.
        var reply = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<html><body>Sign in to venue wifi</body></html>",
                Encoding.UTF8, "text/html"),
        };

        var upload = Uploading(reply);
        var failure = await upload.Should().ThrowAsync<UploadFailedException>();

        failure.Which.IsCredentialFailure.Should().BeFalse(
            "a portal is not the server refusing the token — the desk must keep "
            + "the token and try again later");
        failure.Which.Message.Should().Contain("captive portal");
    }

    [Fact]
    public async Task A_refusal_is_reported_in_the_words_the_server_used()
    {
        // A 403 means this account cannot upload at all and a 401 means the token
        // has expired. Both stop the loop, and an operator handed one canned
        // sentence would chase the wrong one.
        var body = JsonSerializer.Serialize(ApiResult<OfflineBadgeBatchResponse>.Fail(
            new ApiError
            {
                Code = "FORBIDDEN",
                Message = "This account does not hold the offline upload capability.",
                MessageArabic = "لا يملك هذا الحساب صلاحية الرفع.",
            }));
        var reply = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        var upload = Uploading(reply);
        var failure = await upload.Should().ThrowAsync<UploadFailedException>();

        failure.Which.IsCredentialFailure.Should().BeTrue();
        failure.Which.Message.Should().Be(
            "This account does not hold the offline upload capability.");
    }

    [Fact]
    public async Task A_good_answer_still_comes_back_whole()
    {
        // The guard against fixing the failure paths by breaking the working one.
        // The key version is checked because it is what warns a desk provisioned
        // with the wrong badge key, and it now has to survive an upload no human
        // asked for.
        var response = new OfflineBadgeBatchResponse(
            Submitted: 1, Created: 1, PendingApproval: 0, AlreadyUploaded: 0, Rejected: 0,
            Results: [new OfflineBadgeUploadResult(
                3_000_001, "QR1", OfflineBadgeUploadStatus.Created, null, null)],
            ServerBadgeKeyVersion: 4);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ApiResult<OfflineBadgeBatchResponse>.Ok(response)),
                Encoding.UTF8, "application/json"),
        });
        var client = new UploadClient(new HttpClient(handler));

        var result = await client.UploadAsync(
            "https://desk.test", "shift-token", "Desk 3", OneRow());

        result.Created.Should().Be(1);
        result.ServerBadgeKeyVersion.Should().Be(4);
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be(
            "shift-token", "the token the operator pasted is what goes on the wire");
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be(
            "/api/v1/admin/offline/batch");
    }
}
