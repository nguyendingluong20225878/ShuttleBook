using Xunit;

// The local PostGIS container is shared by every disposable test database.
// Keep migration and connection-heavy suites sequential to avoid host timeouts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
