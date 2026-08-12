using System.Runtime.CompilerServices;

// SIMF.Api.Tests exercises the two pure helpers behind
// SimfLegacyEnvironmentGuard directly: the name filter and the failure message.
// Verify() itself reads the real machine environment, so asserting on it would
// only pass on an agent that happened to be mis-provisioned - and the message is
// the whole value of the guard, since it is what tells an operator which script
// re-provisions a half-upgraded server.
[assembly: InternalsVisibleTo("SIMF.Api.Tests")]
