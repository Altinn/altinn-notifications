using System.Net;
using Altinn.Authorization.ProblemDetails;

namespace Altinn.Notifications.Core.Errors;

/// <summary>
/// Problem descriptors for the Notifications API.
/// </summary>
public static class Problems
{
    private static readonly ProblemDescriptorFactory _factory
        = ProblemDescriptorFactory.New("NOT");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/> for missing contact information.
    /// </summary>
    public static ProblemDescriptor MissingContactInformation { get; }
        = _factory.Create(1, HttpStatusCode.UnprocessableEntity, "Missing contact information for recipient(s)");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/> for request terminated by client.
    /// </summary>
    /// <remarks>
    /// Uses HTTP status code 499 (non-standard), commonly used to indicate that the client closed the connection
    /// before the server finished processing the request. This typically occurs when the client cancels or times out.
    /// </remarks>
    public static ProblemDescriptor RequestTerminated { get; }
        = _factory.Create(2, (HttpStatusCode)499, "The client disconnected or cancelled the request before the server could complete processing");

    /// <summary>
    /// Gets a <see cref="ProblemDescriptor"/> for shipment not found.
    /// </summary>
    public static ProblemDescriptor ShipmentNotFound { get; }
        = _factory.Create(3, HttpStatusCode.NotFound, "Shipment not found");

    /// <summary>
    /// Gets a descriptor indicating that a required platform dependency is currently unavailable.
    /// </summary>
    public static ProblemDescriptor PlatformDependencyError { get; }
        = _factory.Create(4, HttpStatusCode.ServiceUnavailable, "A platform dependency is currently unavailable");
}
