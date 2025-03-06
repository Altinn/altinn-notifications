using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.NewApiDemo.api.order.Request;

    public class AssociationDialogporten
    {
        
        [Description("Value of the key 'urn:altinn:dialogporten:dialog-id' pointing to a corresponding dialogue in Dialogporten")]
        [JsonPropertyName("dialogueId")]
        public string? DialogId { get; set; }
        
        [Description("Value of the key 'urn:altinn:dialogporten:transmission-id' pointing to a corresponding transmission in Dialogporten")]
        [JsonPropertyName("transmissionId")]
        public string? TransmissionId { get; set; }
    }

