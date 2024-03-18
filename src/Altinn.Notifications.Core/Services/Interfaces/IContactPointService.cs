using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Service for retrieving contact points for recipients
    /// </summary>
    public interface IContactPointService
    {
        /// <summary>
        /// Looks up and adds the email contact points for recipients based on their national identity number or organisation number
        /// </summary>
        /// <param name="recipients">List of recipients to retrieve contact points for</param>
        /// <returns>The list of recipients augumented with email address points where available</returns>
        /// <remarks>Implementation alters the recipient reference object directly</remarks>
        public Task AddEmailContactPoints(List<Recipient> recipients);

        /// <summary>
        /// Looks up and adds the SMS contact points for recipients based on their national identity number or organisation number
        /// </summary>
        /// <param name="recipients">List of recipients to retrieve contact points for</param>
        /// <returns>The list of recipients augumented with SMS address points where available</returns>
        /// <remarks>Implementation alters the recipient reference object directly</remarks>
        public Task AddSmsContactPoints(List<Recipient> recipients);

        /// <summary>
        /// Retrieves the availabililty of contact points for the provided recipient based on their national identity number or organisation number
        /// </summary>
        /// <param name="recipients">List of recipients to check contact point availability for</param>
        /// <returns>The list of recipients with contact point availability details</returns>
        public Task<List<UserContactPointAvailability>> GetContactPointAvailability(List<Recipient> recipients);
    }
}
