using Xunit;

// D-194 — disable cross-class parallelism in this assembly. Page tests that
// exercise the Arabic render mutate the process-global
// CultureInfo.CurrentUICulture (restored in a finally); CurrentUICulture is
// thread-affine, so a concurrently-scheduled test in another class could
// observe the "ar" window before the restore. The sequential default removes
// that isolation hazard (mirrors SIMF.Api.Tests).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
