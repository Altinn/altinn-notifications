using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models
{
    /// <summary>
    /// Class representing an email
    /// </summary>
    public class EMail
    {
        /// <summary>
        /// Gets the id of the email.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the subject of the email.
        /// </summary>
        public string Subject { get; private set; }

        /// <summary>
        /// Gets the body of the email.
        /// </summary>
        public string Body { get; private set; }

        /// <summary>
        /// Gets the to adress of the email.
        /// </summary>
        public string ToAddress { get; private set; }

        /// <summary>
        /// Gets the content type of the email.
        /// </summary>
        public EMailContentType ContentType { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EMail"/> class.
        /// </summary>
        public EMail(string id, string subject, string body, string toAddress, EMailContentType contentType)
        {
            Id = id;
            Subject = subject;
            Body = body;
            ToAddress = toAddress;
            ContentType = contentType;
        }
    }
}