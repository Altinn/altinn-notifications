namespace Altinn.Notifications.Core.Models;

/// <summary>
/// A class representing a service error object used to transfere error information from service to controller.
/// </summary>
public class ServiceError
{
    /// <summary>
    /// The error code
    /// </summary>
    /// <remarks>The error code must be a valid HTTP status code value.</remarks>
    public int ErrorCode { get; private set; }

    /// <summary>
    /// The error message
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Dictionary with more property specific details.
    /// </summary>
    public Dictionary<string, string> Errors { get; private set; } = new();

    /// <summary>
    /// Create a new instance of a service error
    /// </summary>
    public ServiceError(int errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Create a new instance of a service error
    /// </summary>
    public ServiceError(int errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Create a new instance of a service error with error code 400 and the given key and message.
    /// </summary>
    public ServiceError(string errorKey, string errorMessage)
    {
        ErrorCode = 400;
        Errors.Add(errorKey, errorMessage);
    }
}
