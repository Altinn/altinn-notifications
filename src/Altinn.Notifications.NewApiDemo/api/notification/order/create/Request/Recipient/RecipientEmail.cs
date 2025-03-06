using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Notifications.NewApiDemo.api.Recipient;

using System.Text.Json.Serialization;

public class RecipientEmail
{
    [Description("The destination email address")]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }
    
    [JsonPropertyName("emailSettings")]
    [Required] //This is in place instead of a non-nullable required (as EmailAddress), to avoid OpenAPI generating a non-nullable EmailSettings2 contract...
    public EmailSettings? EmailSettings { get; set; }
    
}
