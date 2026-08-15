namespace SIMF.Common;

/// <summary>The Organization Profile is a singleton row with a fixed id.
///
/// <para>It lives in <c>SIMF.Common</c> for the same reason
/// <see cref="OrganizationHeroVideoRoute"/> does: two processes need it and
/// neither can reference the other. The API owns the entity, and the Control
/// Panel — which references only the shared projects, never the domain — needs
/// the id to address the profile's own assets, the logo being owned by the
/// profile rather than by anything the CP has a row for.</para>
///
/// <para>The alternative was a second copy of the literal in the Control Panel,
/// which is precisely the arrangement the hero-video route was pulled into this
/// assembly to end: nothing detects two copies drifting apart, and the symptom
/// is a control that quietly addresses the wrong owner.</para></summary>
public static class OrganizationProfileIds
{
    /// <summary>The one and only Organization Profile row.</summary>
    public static readonly Guid Singleton =
        Guid.Parse("00000000-0000-0000-0000-000000000003");
}
