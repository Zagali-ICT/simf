namespace SIMF.Common.Enums;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — what a <c>Hall</c> is designated for. The
/// owner's model is "a hall for booths, another for sessions, another for
/// meetings — the system must be flexible".
///
/// <para><see cref="General"/> (value 0) is the default every pre-existing hall
/// carries after the additive migration: a General hall is un-specialised and may
/// host anything (it preserves the prior behaviour where a hall could back both
/// <c>Session.HallId</c> and <c>Booth.HallId</c>). The other values mark a hall as
/// dedicated to a single purpose so the Control Panel can show the right layout +
/// allocation tools (seat grid for Session, meeting tables for Meeting, booth
/// placement for Booth).</para>
/// </summary>
public enum HallPurpose
{
    General = 0,
    Booth = 1,
    Session = 2,
    Meeting = 3,
}
