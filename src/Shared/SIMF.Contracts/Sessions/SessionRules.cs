namespace SIMF.Contracts.Sessions;

/// <summary>
/// The session field limits, in one place. The admin service raises the 400,
/// the EF mapping sizes the column and the Control Panel form pre-checks the
/// input, and all three had the same numbers written out separately - so a
/// limit could be widened in one and left behind in the others.
/// </summary>
public static class SessionRules
{
    public const int MinCodeLength = 2;
    public const int MaxCodeLength = 16;

    public const int MinTitleLength = 1;
    public const int MaxTitleLength = 256;
}
