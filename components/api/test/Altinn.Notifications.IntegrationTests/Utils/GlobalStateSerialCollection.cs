using Xunit;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Test collection for tests that read or mutate database state that is global rather than
/// scoped to test-specific data (e.g. whole-table deletes on <c>resourcelimitlog</c> or
/// assertions on the append-only <c>statusfeed</c> sequence). The collection is excluded
/// from parallelization so these tests run serially after all parallel collections complete,
/// without interference from concurrently running tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class GlobalStateSerialCollection
{
    /// <summary>
    /// The collection name used in <see cref="CollectionAttribute"/> on test classes.
    /// </summary>
    public const string Name = "GlobalStateSerial";
}
