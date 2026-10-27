using Altinn.Notifications.IntegrationTests.Utils;

using Xunit;

[assembly: AssemblyFixture(typeof(PostgreSqlMigrationFixture))]

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Assembly-level fixture that runs the Yuniql database migrations exactly once before any
/// test executes. Each test host started via WebApplicationFactory also runs migrations on
/// startup; against an already-migrated database those are no-op version checks, but against
/// an empty database (a fresh CI run) parallel host startups would race on creating the
/// schema. Migrating up front removes that race.
/// </summary>
public sealed class PostgreSqlMigrationFixture
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlMigrationFixture"/> class
    /// and migrates the database.
    /// </summary>
    public PostgreSqlMigrationFixture()
    {
        ServiceUtil.EnsureDatabaseMigrated();
    }
}
