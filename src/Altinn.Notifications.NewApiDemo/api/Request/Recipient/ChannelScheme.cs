namespace Altinn.Notifications.NewApiDemo.api.Recipient;

using System.Text.Json.Serialization;
using System.ComponentModel;

[JsonConverter(typeof(JsonStringEnumConverter<ChannelScheme>))]
public enum ChannelScheme
{
    EmailPreferred,
    SmsPreferred,
    EmailOnly,
    SMSOnly
}
