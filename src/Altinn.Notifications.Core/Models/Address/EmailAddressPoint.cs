using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Address
{
    /// <summary>
    /// A class represeting an address point
    /// </summary>
    public class EmailAddressPoint : IAddressPoint
    {
        /// <inheritdoc/>
        public AddressType AddressType { get; set; }

        /// <summary>
        /// Gets or sets the email address
        /// </summary>
        public string EmailAddress { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailAddressPoint"/> class.
        /// </summary>
        public EmailAddressPoint(string emailAddress)
        {
            AddressType = AddressType.Email;
            EmailAddress = emailAddress;
        }
    }
}