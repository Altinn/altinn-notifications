using Altinn.Notifications.IntegrationTests.Utils;

using Xunit;

[assembly: AssemblyFixture(typeof(PostgreSqlMigrationFixture))]

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Assembly-level fixture that runs the Yuniql database migrations exactly once before any
/// test executes. Test hosts started via WebApplicationFactory do not migrate on startup
/// (<c>PostgreSQLSettings:EnableDBConnection</c> is <c>false</c>): Yuniql keeps its
/// configuration in a process-wide singleton, so hosts starting in parallel would race on it
/// and could run the migration without a connection string.
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
