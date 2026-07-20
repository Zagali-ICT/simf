// Tests: SIMF.Application.Email.EmailMessage — D-751 optional Attachments default.
using SIMF.Application.Email;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// D-751 — the three-argument <see cref="EmailMessage"/> construction must keep
/// working and leave <see cref="EmailMessage.Attachments"/> null, so every
/// pre-existing caller (none of which passed attachments) is unchanged. The
/// four-argument form carries attachments through.
/// </summary>
public class EmailMessageTests
{
    [Fact]
    public void Three_argument_message_has_no_attachments()
    {
        var message = new EmailMessage("to@simf.test", "Subject", "<p>Body</p>");

        Assert.Null(message.Attachments);
    }

    [Fact]
    public void Attachments_round_trip_when_supplied()
    {
        var attachment = new EmailAttachment(
            "badges.zip", "application/zip", new byte[] { 1, 2, 3 });

        var message = new EmailMessage(
            "to@simf.test", "Subject", "<p>Body</p>", new[] { attachment });

        var only = Assert.Single(message.Attachments!);
        Assert.Equal("badges.zip", only.FileName);
        Assert.Equal("application/zip", only.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, only.Content);
    }
}
