using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Parties;

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
    /// Looks up and adds the preferred contact points for recipients based on their national identity number or organization number.
    /// </summary>
    /// <param name="channel">The notification channel specifying which channel is preferred.</param>
    /// <param name="recipients">List of recipients to retrieve contact points for.</param>
    /// <param name="resourceId">The resource to find contact points in relation to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Implementation alters the recipient reference object directly.</remarks>
    public Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId);

    /// <summary>
    /// Adds the name components (e.g., name, first name, middle name, last name) to the specified recipients.
    /// </summary>
    /// <param name="recipients">
    /// A list of <see cref="Recipient"/> objects to which the name components will be added. 
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to add name components to the recipients.
    /// </returns>
    public Task AddRecipientNameComponents(List<Recipient> recipients);
}
