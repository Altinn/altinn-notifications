using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Represents errors that occur during recipient lookup operations.
/// </summary>
/// 
[ExcludeFromCodeCoverage]
public class RecipientLookupException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientLookupException"/> class.
    /// </summary>
    public RecipientLookupException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientLookupException"/> class with a specified exception message
    /// </summary>
    /// <param name="message">Message describing the error</param>
    public RecipientLookupException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipientLookupException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">Message describing the error</param>
    /// <param name="innerException">>The exception that is the cause of the current exception, or a null reference if no inner exception is specified</param>
    public RecipientLookupException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
