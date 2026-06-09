using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Exceptions;

/// <summary>
/// Base class for exceptions that occur when updating the send status of Email or SMS notifications.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class SendStatusUpdateException : Exception
{
    /// <summary>
    /// The notification channel the update concerned (Email or Sms).
    /// </summary>
    public NotificationChannel Channel { get; }

    /// <summary>
    /// The value of the identifier that was not matched.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// The type of the identifier that was not matched.
    /// </summary>
    public SendStatusIdentifierType IdentifierType { get; }

    /// <summary>
    /// Initializes a new instance of a class derived from <see cref="SendStatusUpdateException"/>.
    /// </summary>
    /// <param name="channel">The notification channel the update concerned (Email or Sms).</param>
    /// <param name="identifier">The value of the identifier.</param>
    /// <param name="identifierType">The type of the identifier.</param>
    /// <param name="message">The exception message.</param>
    protected SendStatusUpdateException(
        NotificationChannel channel,
        string identifier,
        SendStatusIdentifierType identifierType,
        string message)
        : base(message)
    {
        Channel = channel;
        Identifier = identifier;
        IdentifierType = identifierType;
    }
}
