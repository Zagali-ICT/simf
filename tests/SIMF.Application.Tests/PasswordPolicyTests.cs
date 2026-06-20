using SIMF.Common;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// NCA Secure App-Dev Standard A7-10/A7-28/A7-29 — proves the shared password
/// policy accepts strong passphrases and rejects the structural weaknesses the
/// standard names (missing classes, repeats, sequences, common/leet passwords,
/// the user's own identifier).
/// </summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Zx9#mKp2!")]   // the test-suite password
    [InlineData("Newp@ssw0rd!")] // leet but not a single dictionary base word
    [InlineData("Tr0ub4dor&3xkcd")]
    public void Strong_passwords_pass_every_check(string password)
    {
        Assert.True(PasswordPolicy.HasUppercase(password));
        Assert.True(PasswordPolicy.HasLowercase(password));
        Assert.True(PasswordPolicy.HasDigit(password));
        Assert.True(PasswordPolicy.HasSpecial(password));
        Assert.False(PasswordPolicy.HasRepeatRun(password));
        Assert.False(PasswordPolicy.HasSequentialRun(password));
        Assert.False(PasswordPolicy.IsCommon(password));
    }

    [Theory]
    [InlineData("password")]    // exact dictionary base word
    [InlineData("Passw0rd!")]   // leet spelling of "password"
    [InlineData("P@ssw0rd")]    // more leet
    [InlineData("123456")]      // exact common raw password
    [InlineData("qwerty123")]   // exact common raw password
    public void Common_and_leet_passwords_are_rejected(string password) =>
        Assert.True(PasswordPolicy.IsCommon(password));

    [Theory]
    [InlineData("aaa123!X")]    // three identical in a row
    [InlineData("Xy111zZ!")]
    public void Three_identical_in_a_row_is_a_repeat_run(string password) =>
        Assert.True(PasswordPolicy.HasRepeatRun(password));

    [Theory]
    [InlineData("Xy123zZ!")]    // 123
    [InlineData("Qabc99!Z")]    // abc
    [InlineData("Zy987!aB")]    // 987 descending
    public void Three_sequential_in_a_row_is_a_sequence(string password) =>
        Assert.True(PasswordPolicy.HasSequentialRun(password));

    [Fact]
    public void Two_identical_in_a_row_is_allowed()
    {
        Assert.False(PasswordPolicy.HasRepeatRun("Zx9#mKp2!"));
        Assert.False(PasswordPolicy.HasRepeatRun("aa12bB!z")); // "aa" is only two
    }

    [Theory]
    [InlineData("user@simf.test", "user@simf.test")] // equals the whole email
    [InlineData("ahmad.salem", "ahmad.salem@simf.test")] // equals the local part
    public void Password_matching_the_identifier_is_rejected(string password, string email) =>
        Assert.True(PasswordPolicy.ResemblesIdentifier(password, email));

    [Fact]
    public void Unrelated_password_does_not_resemble_the_identifier() =>
        Assert.False(PasswordPolicy.ResemblesIdentifier("Zx9#mKp2!", "ahmad.salem@simf.test"));
}
