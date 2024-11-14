namespace Altinn.Notifications.Integrations.Profile.Models;

/// <summary>
/// DTO for user contact points received from the ProfileClient, describing the availability of contact points for a user
/// </summary>
public class UserContactPointsDto
{
    /// <summary>
    /// Gets or sets the ID of the user
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets the national identityt number of the user
    /// </summary>
    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether the user has reserved themselves from electronic communication
    /// </summary>
    public bool IsReserved { get; set; }

    /// <summary>
    /// Gets or sets the mobile number
    /// </summary>
    public string? MobileNumber { get; set; }

    /// <summary>
    /// Gets or sets the email address
    /// </summary>
    public string? Email { get; set; }
}
