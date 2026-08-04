// Tests: SIMF.Api/Endpoints/Admin/SessionEndpoints.cs
//
// D-842 — a structural ratchet over `UpdateSessionRequest`, the route DTO that
// `UpdateSessionEndpoint` binds instead of its contract type.
//
// The codebase already HAS a structural remedy for this bug class, and it is not
// this file. D-505 established a derived route that INHERITS the contract:
//
//     public sealed class UpdateHallRoute : AdminUpdateHallRequest
//     { public Guid Id { get; set; } }
//
// with the comment "Passing the bound req straight through makes the drop
// impossible." It was introduced for the same failure: the old inline
// `UpdateHallRequest` omitted the GPS geofence fields, so a PUT wiped the stored
// geofence on every edit.
//
// Under src/Backend/SIMF.Api/Endpoints/Admin as of D-843: TEN route DTOs use that
// inheriting shape and are safe by construction; NINE still re-declare their
// fields and hand-copy them in HandleAsync, and are not. (D-843 moved two from the
// second group to the first, so it was 8/11 before.)
//
// `UpdateSessionRequest` is one of the eleven. Its hand-copy had silently dropped
// a field four times before D-842 — the live URLs (D-439), `Type`,
// `SeatSelectionModeOverride` (D-485) and `Language`/`Outcomes` — each found by
// hand, in production behaviour, after shipping. D-842 was the fifth:
// `ArrivalGraceMinutesOverride`.
//
// The failure is invisible by construction. Adding a property to the contract
// compiles, the PUT still returns 200, and the endpoint writes the default over
// the stored value: no build error, no failing test, no audit entry.
//
// So this file is the SECOND-best fix, and deliberately so: converting the
// endpoint to the D-505 inheriting shape is the real remedy and is proposed
// separately, because it deletes the whole mapping block and is a different
// change from the one-field fix D-842 was approved as. Until that lands, this
// asserts the parity the hand-copy is supposed to maintain.
//
// It is also scoped to ONE of the nine. The D-842 audit diffed the others and
// found two more live drops, both since fixed by CONVERSION rather than by another
// ratchet (D-843): `UpdateGateRequest` was losing `Gate.HallId`, unbinding a hall
// door so it stopped feeding hall attendance; and `UpdateAdminProfileTypeRouteRequest`
// was losing `ShowInPartnerDirectory`, whose contract default is `true`, so that
// drop failed OPEN and silently re-exposed a type in the partner directory.
//
// Those two now inherit, which is why the count above moved 10/11 -> 12/9. The
// remaining nine are still hand-copies; converting sessions would leave eight and
// delete this file.
//
// It pins SHAPE, not behaviour: it proves a field cannot go missing, which is the
// mistake that keeps being made. It does NOT prove the mapping block assigns each
// field — that is behavioural and lives with the feature (ArrivalGraceResolutionTests).
using System.Reflection;
using SIMF.Api.Endpoints.Admin;
using SIMF.Contracts.Admin;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class UpdateSessionRequestParityTests
{
    /// <summary>Properties the route DTO may carry that the contract does not,
    /// each with the reason it is legitimately route-bound rather than a field the
    /// endpoint forgot to pass on.</summary>
    private static readonly Dictionary<string, string> RouteOnly = new(StringComparer.Ordinal)
    {
        ["Id"] = "Bound from the {id:guid} route segment; the service takes it as a "
                 + "separate argument, so the contract request has no Id of its own.",
    };

    [Fact]
    public void Every_contract_field_is_carried_on_the_route_dto()
    {
        var contractFields = Fields(typeof(AdminUpdateSessionRequest));
        var routeFields = Fields(typeof(UpdateSessionRequest));

        // A sweep that matches nothing would pass silently — that is a broken
        // enumeration, not parity.
        Assert.True(
            contractFields.Count > 0 && routeFields.Count > 0,
            "One of the two DTOs enumerated no settable properties at all — the "
            + "reflection is broken, not the parity.");

        var missing = contractFields
            .Where(field => !routeFields.ContainsKey(field.Key))
            .Select(field => field.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"UpdateSessionRequest is missing {missing.Length} field(s) that "
            + $"AdminUpdateSessionRequest declares: {string.Join(", ", missing)}. "
            + "A PUT will bind them to their default and the mapping in "
            + "UpdateSessionEndpoint will write that default over the stored value, "
            + "silently. Add the propert(y/ies) to UpdateSessionRequest AND the "
            + "corresponding line(s) to the `new AdminUpdateSessionRequest { … }` "
            + "block, then cover the round-trip behaviourally. Better: convert the "
            + "endpoint to the D-505 shape (UpdateSessionRoute : "
            + "AdminUpdateSessionRequest) so the drop becomes impossible.");
    }

    [Fact]
    public void The_carried_fields_agree_on_type()
    {
        // A name match with a different type is the same bug wearing a disguise:
        // `int?` against `int` turns "inherit" into 0, and a widened enum silently
        // truncates. Both compile.
        var routeFields = Fields(typeof(UpdateSessionRequest));

        var mismatched = Fields(typeof(AdminUpdateSessionRequest))
            .Where(field => routeFields.TryGetValue(field.Key, out var routeType)
                            && routeType != field.Value)
            .Select(field => $"{field.Key}: contract {Name(field.Value)} vs route "
                             + $"{Name(routeFields[field.Key])}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            mismatched.Length == 0,
            "UpdateSessionRequest disagrees with AdminUpdateSessionRequest on the "
            + $"type of: {string.Join("; ", mismatched)}.");
    }

    [Fact]
    public void The_route_dto_carries_nothing_the_contract_does_not()
    {
        // The other direction, so the route DTO cannot quietly grow a field that
        // reaches no further than the endpoint — a setting an admin can send and
        // watch have no effect.
        var contractFields = Fields(typeof(AdminUpdateSessionRequest));

        var extra = Fields(typeof(UpdateSessionRequest))
            .Where(field => !contractFields.ContainsKey(field.Key)
                            && !RouteOnly.ContainsKey(field.Key))
            .Select(field => field.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            extra.Length == 0,
            $"UpdateSessionRequest declares {string.Join(", ", extra)}, which "
            + "AdminUpdateSessionRequest does not. The endpoint cannot pass it on, "
            + "so a client that sets it gets a 200 and no effect. Either add it to "
            + "the contract and map it, or drop it — or add it to RouteOnly with "
            + "the reason it is genuinely route-bound, as Id is.");
    }

    [Fact]
    public void The_route_only_allow_list_has_no_stale_entries()
    {
        // An allow-list that outlives the property it excuses quietly widens the
        // hole the other three tests are guarding.
        var routeFields = Fields(typeof(UpdateSessionRequest));

        var stale = RouteOnly.Keys
            .Where(name => !routeFields.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"RouteOnly excuses {string.Join(", ", stale)}, which UpdateSessionRequest "
            + "no longer declares. Remove the entr(y/ies).");
    }

    private static Dictionary<string, Type> Fields(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite)
            .ToDictionary(property => property.Name, property => property.PropertyType,
                          StringComparer.Ordinal);

    private static string Name(Type type) =>
        Nullable.GetUnderlyingType(type) is { } inner ? $"{inner.Name}?" : type.Name;
}
