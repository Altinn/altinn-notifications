namespace Altinn.Notifications.Core.Shared;

/// <summary>
/// A class representing a service error object used to transfere error information from service to controller.
/// </summary>
public class ServiceError
{
    /// <summary>
    /// The error code
    /// </summary>
    /// <remarks>An error code translates directly into an HTTP status code</remarks>
    public int ErrorCode { get; private set; }

    /// <summary>
    /// The error message
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// The error type for machine-readable error identification
    /// </summary>
    public string? ErrorType { get; private set; }

    /// <summary>
    /// Create a new instance of a service error
    /// </summary>
    public ServiceError(int errorCode, string errorMessage, string? errorType = null)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    /// <summary>
    /// Create a new instance of a service error
    /// </summary>
    public ServiceError(int errorCode, string? errorType = null)
    {
        ErrorCode = errorCode;
        ErrorType = errorType;
    }
}
