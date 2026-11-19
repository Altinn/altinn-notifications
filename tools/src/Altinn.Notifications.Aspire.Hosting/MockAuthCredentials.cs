using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// The Basic auth the mock token issuer takes on its token endpoints. Read the password with
/// <see cref="ParameterResource.GetValueAsync(CancellationToken)"/> once the app host runs, e.g. in a <c>BeforeStartEvent</c>.
/// </summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The password, generated for the checkout.</param>
public sealed record MockAuthCredentials(string Username, ParameterResource Password) : IResourceAnnotation;
