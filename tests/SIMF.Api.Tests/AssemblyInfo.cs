using Xunit;

// The integration tests share process-wide environment variables and a SQL
// Server LocalDB instance, so test collections run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
