using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core.Models
{
    public class Target
    {
        public int Id { get; set; }

        public int NotificationId { get; set; }
        
        /// <summary>
        /// Possible values:
        /// Email
        /// SMS
        /// PreferredSMS - out of scope
        /// PreferredEmail - out of scope
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Possible values:
        /// Email address
        /// Mobile number
        /// null - Use PartyReference to identify 
        /// </summary>
        public string? Address { get; set; }

        public DateTime? Sent { get; set; }

        public DateTime? Failed { get; set; }

        public string? FailedReason { get; set; }

    }
}
