using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Service for retrieving contact points for recipients.
/// </summary>
public interface IContactPointService
{
    /// <summary>
    /// Looks up and adds the email contact points for recipients based on their national identity number or organization number.
    /// </summary>
    /// <param name="recipients">List of recipients to retrieve contact points for.</param>
    /// <param name="resourceId">The resource to find contact points in relation to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Implementation alters the recipient reference object directly.</remarks>
    public Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId);

    /// <summary>
    /// Looks up and adds the SMS contact points for recipients based on their national identity number or organization number.
    /// </summary>
    /// <param name="recipients">List of recipients to retrieve contact points for.</param>
    /// <param name="resourceId">The resource to find contact points in relation to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Implementation alters the recipient reference object directly.</remarks>
    public Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId);

    /// <summary>
    /// Looks up and adds both email and SMS contact points simultaneously for recipients based on their national identity number or organization number.
    /// </summary>
    /// <param name="recipients">List of recipients to retrieve contact points for. Each recipient must have either a national identity number or organization number.</param>
    /// <param name="resourceId">The resource identifier used to determine relevant contact points within the context of a specific resource or service.</param>
    /// <returns>Implementation alters the recipient reference objects directly by adding any found email and SMS contact points to their <see cref="Recipient.AddressInfo"/> collection.</returns>
    public Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId);

    /// <summary>
    /// Looks up and adds the preferred contact points for recipients based on their national identity number or organization number.
    /// </summary>
    /// <param name="channel">The notification channel specifying which channel is preferred.</param>
    /// <param name="recipients">List of recipients to retrieve contact points for.</param>
    /// <param name="resourceId">The resource to find contact points in relation to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Implementation alters the recipient reference object directly.</remarks>
    public Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId);
}
