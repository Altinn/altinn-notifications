namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

[JsonConverter(typeof(JsonStringEnumConverter<ChannelScheme>))]
public enum ChannelScheme
{
    EmailPreferred,
    SMSPreferred,
    EmailOnly,
    SMSOnly
}