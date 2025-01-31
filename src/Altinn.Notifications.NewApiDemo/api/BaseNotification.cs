using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WebApplication1
{
    public class BaseNotification
    {
        [Description("Reference determined bt the sender. May be unique or non-unique")]
        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }
        
        [JsonPropertyName("recipient")]
        public required Recipient Recipient {get;set;}
    }
}
