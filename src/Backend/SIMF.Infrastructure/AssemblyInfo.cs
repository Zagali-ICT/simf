using System.Runtime.CompilerServices;

// SIMF.Api.Tests exercises internal infrastructure helpers (TotpVerifier — the
// secret-normalisation fix for myComment #35) directly. The rest of the API
// is exercised via the public HTTP surface.
[assembly: InternalsVisibleTo("SIMF.Api.Tests")]
