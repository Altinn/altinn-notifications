using Xunit;

namespace Altinn.Notifications.Tools.Tests.Infrastructure;

/// <summary>
/// xUnit collection definition that shares a single <see cref="IntegrationContainersFixture"/>
/// across all tools integration tests, so the PostgreSQL and Service Bus emulator containers
/// are started once and reused for the full test run.
/// </summary>
[CollectionDefinition(nameof(IntegrationContainersCollection))]
public class IntegrationContainersCollection : ICollectionFixture<IntegrationContainersFixture>
{
}
