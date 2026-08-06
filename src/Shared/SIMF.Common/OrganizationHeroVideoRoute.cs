namespace SIMF.Common;

/// <summary>
/// The public route the uploaded hero background video is served from.
///
/// <para>It lives in <c>SIMF.Common</c> rather than beside the endpoint because
/// TWO processes need it and they cannot reference each other: the API registers
/// the route and composes the absolute URL it persists, and the Control Panel
/// tests the stored <c>BackgroundVideoUrl</c> against it to decide whether the
/// current hero is an uploaded file (offer "Remove") or a pasted external link
/// (do not).</para>
///
/// <para>D-841 — the Control Panel used to hold its own copy of this string,
/// under a comment asking the next person to keep the two equal by hand and
/// warning that "if the served route changes, the Remove affordance silently
/// stops appearing". Nothing detected the drift: no build error, no failing
/// test, just a button that quietly stops being drawn. One constant, referenced
/// by both, removes the hazard rather than documenting it.</para>
///
/// <para>The <c>.mp4</c> suffix is load-bearing: it is what makes the composed
/// absolute URL pass the app and website hero accept-gate
/// (<see cref="LiveStreamUrlPolicy"/> — an https URL whose path ends
/// <c>.mp4</c>), which is why that policy lives in this same assembly.</para>
/// </summary>
public static class OrganizationHeroVideoRoute
{
    /// <summary>The public stream route, RELATIVE to the FastEndpoints
    /// <c>RoutePrefix</c> ("api/v1").</summary>
    public const string StreamRoute = "/app/organization/hero-video.mp4";
}
