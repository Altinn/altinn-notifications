using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// Credentials the mocks and the services share, made for each checkout instead of committed.
/// </summary>
internal static class GeneratedSecrets
{
    /// <summary>
    /// Adds a secret parameter of <paramref name="length"/> letters and digits. Aspire makes it on the app host's first run and keeps
    /// it in the app host's Aspire secrets (<c>aspire secret list</c>), so it stays the same between runs and files written from it
    /// for Bruno and k6 stay valid.
    /// </summary>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The parameter name. The value is in the secret <c>Parameters:&lt;name&gt;</c>.</param>
    /// <param name="length">The number of characters.</param>
    public static IResourceBuilder<ParameterResource> AddGeneratedSecret(this IDistributedApplicationBuilder builder, string name, int length) =>
        builder.AddParameter(name, new GenerateParameterDefault { MinLength = length, Special = false }, secret: true, persist: true);
}
