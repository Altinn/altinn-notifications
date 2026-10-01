using Xunit;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

/// <summary>
/// xUnit collection definition that shares the test containers fixture across all tests in the collection.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestContainersCollection))]
public class IntegrationTestContainersCollection : ICollectionFixture<IntegrationTestSmsAsbContainersFixture>
{
}
