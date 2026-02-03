namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// Exception thrown when a platform dependency (ProfileClient, AuthorizationService, etc.) fails.
/// Wraps all failure types including HTTP errors, network failures, timeouts, and cancellations.
/// </summary>
public class PlatformDependencyException : Exception
{
    /// <summary>
    /// The name of the dependency that failed (e.g., "ProfileClient", "AuthorizationService")
    /// </summary>
    public string DependencyName { get; }

    /// <summary>
    /// The operation that was being performed when the failure occurred
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Indicates if the failure was due to a timeout or cancellation
    /// </summary>
    public bool IsTransient => InnerException is OperationCanceledException or HttpRequestException;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformDependencyException"/> class.
    /// </summary>
    /// <param name="dependencyName">The name of the dependency that failed</param>
    /// <param name="operation">The operation that was being performed when the failure occurred</param>
    /// <param name="innerException">The original exception that was thrown</param>
    public PlatformDependencyException(string dependencyName, string operation, Exception innerException)
        : base($"Platform dependency '{dependencyName}' failed during '{operation}': {innerException.Message}", innerException)
    {
        DependencyName = dependencyName;
        Operation = operation;
    }
}
