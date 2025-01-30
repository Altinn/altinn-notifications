using System.ComponentModel;

namespace WebApplication1;

using System.Text.Json.Serialization;

public class RecipientEmail: Recipient
{
    [Description("The destination email address")]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }
    
    [JsonPropertyName("emailSettings")]
    public required EmailSettings EmailSettings { get; set; }
    
}