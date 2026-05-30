using Xunit;

// D-194 — disable cross-class parallelism in this assembly. The
// AccountStateBannerTests Arabic-culture cases mutate the process-global
// CultureInfo.CurrentUICulture (restored in a finally); CurrentUICulture
// is thread-affine, so a concurrently-scheduled test in another class
// could observe the "ar" window before the restore. Today no other test
// reads ambient culture, but the sequential default removes the latent
// isolation hazard (mirrors SIMF.Api.Tests).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
