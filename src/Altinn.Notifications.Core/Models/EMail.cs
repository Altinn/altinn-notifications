using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models
{
    /// <summary>
    /// Class representing an email
    /// </summary>
    public class EMail
    {
        /// <summary>
        /// Gets or sets the id of the email.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the subject of the email.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the body of the email.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets the to adress of the email.
        /// </summary>
        public string ToAddress { get; set; }

        /// <summary>
        /// Gets or sets the content type of the email.
        /// </summary>
        public EMailContentType ContentType { get; set; }
    }
}