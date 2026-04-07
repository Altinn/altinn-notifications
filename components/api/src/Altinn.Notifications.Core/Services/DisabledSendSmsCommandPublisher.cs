using System.Diagnostics.CodeAnalysis;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Represents a command publisher that does not send SMS messages.
/// </summary>
/// <remarks>This implementation of ISendSmsPublisher is disabled and will always throw a
/// NotImplementedException when attempting to publish an SMS command. Use this class when SMS publishing functionality
/// should be explicitly disabled, such as in testing or development environments.</remarks>
[ExcludeFromCodeCoverage]
public class DisabledSendSmsCommandPublisher : ISendSmsPublisher
{
    /// <summary>
    /// This method is intentionally not implemented and will throw an exception if called, indicating that the SMS command publisher is disabled.
    /// </summary>
    /// <param name="smsList">The collection of SMS notifications to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with a read-only list of <see cref="Sms"/> objects for notifications
    /// that failed to deliver. An empty list indicates that all notifications were delivered successfully.
    /// </returns>
    /// <exception cref="InvalidOperationException">Will always throw an InvalidOperationException when called.</exception>
    public Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
         $"{nameof(DisabledSendSmsCommandPublisher)} was called for notification {sms.NotificationId}, " +
         "which means 'EnableSendSmsCommandPublisher' is true but Wolverine is not configured. " +
         "Either set 'WolverineSettings:EnableWolverine' to true, or set 'NotificationConfig:EnableSendSmsCommandPublisher' to false to use Kafka.");
    }

    /// <summary>
    /// This method is intentionally not implemented and will throw an exception if called, indicating that the SMS command publisher is disabled.
    /// </summary>
    /// <param name="sms">The SMS notification to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. Always throws an <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Will always throw an InvalidOperationException when called.</exception>
    Task<Sms?> ISendSmsPublisher.PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
         $"{nameof(DisabledSendSmsCommandPublisher)} was called for notification {sms.NotificationId}, " +
         "which means 'EnableSendSmsCommandPublisher' is true but Wolverine is not configured. " +
         "Either set 'WolverineSettings:EnableWolverine' to true, or set 'NotificationConfig:EnableSendSmsCommandPublisher' to false to use Kafka.");
    }
}
