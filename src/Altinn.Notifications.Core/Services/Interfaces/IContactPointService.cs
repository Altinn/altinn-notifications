using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Service for retrieving contact points for recipients
    /// </summary>
    public interface IContactPointService
    {
        /// <summary>
        /// Retrieves email contact points for recipients based on their national identity number or organisation number
        /// </summary>
        /// <param name="recipients">List of recipients to retrieve contact points for</param>
        /// <returns>The list of recipients augumented with email address points where available</returns>
        public Task<List<Recipient>> GetEmailContactPoints(List<Recipient> recipients);

        /// <summary>
        /// Retrieves SMS contact points for recipients based on their national identity number or organisation number
        /// </summary>
        /// <param name="recipients">List of recipients to retrieve contact points for</param>
        /// <returns>The list of recipients augumented with SMS address points where available</returns>
        public Task<List<Recipient>> GetSmsContactPoints(List<Recipient> recipients);
    }
}
