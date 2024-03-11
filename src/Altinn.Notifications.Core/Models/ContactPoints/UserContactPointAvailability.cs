namespace Altinn.Notifications.Core.Models.ContactPoints
{
    /// <summary>
    /// Class describing the contact points of a user
    /// </summary>
    public class UserContactPointAvailability
    {
        /// <summary>
        /// Gets or sets the ID of the user
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Gets or sets the national identityt number of the user
        /// </summary>
        public string NationalIdentityNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a boolean indicating whether the user has reserved themselves from electronic communication
        /// </summary>
        public bool IsReserved { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating whether the user has registered a mobile number
        /// </summary>
        public bool MobileNumberRegistered { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating whether the user has registered an email address
        /// </summary>
        public bool EmailRegistered { get; set; }
    }
}
