#nullable enable
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Xunit;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

/// <summary>
/// xUnit collection definition for integration tests that require infrastructure containers.
/// All tests in this collection share the same container instances (PostgreSQL, MSSQL, Service Bus Emulator).
/// </summary>
[CollectionDefinition(nameof(IntegrationTestContainersCollection))]
public class IntegrationTestContainersCollection : ICollectionFixture<IntegrationTestContainersFixture>
{
}
