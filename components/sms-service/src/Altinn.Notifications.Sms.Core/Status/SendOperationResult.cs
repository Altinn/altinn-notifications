using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Sms.Core.Status;

/// <summary>
/// A class representing a send operation update object
/// </summary>
public class SendOperationResult
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    /// <summary>
    /// The notification id
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// The reference to the sending in sms gateway
    /// </summary>
    public string GatewayReference { get; set; } = string.Empty;

    /// <summary>
    /// The sms send result
    /// </summary>
    public SmsSendResult? SendResult { get; set; }

    /// <summary>
    /// Json serializes the <see cref="SendOperationResult"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, _serializerOptions);
    }
}
