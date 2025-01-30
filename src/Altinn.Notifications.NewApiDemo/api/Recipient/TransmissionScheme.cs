

namespace WebApplication1;

using System.Text.Json.Serialization;
using System.ComponentModel;

[JsonConverter(typeof(JsonStringEnumConverter<TransmissionScheme>))]
[Description("See functional doc for more details on each sending-scheme")]
public enum TransmissionScheme
{
    [Description("Schedules the message to be sent. Email and SMS is only sent during daytime on working-days.")]
    Daytime_Workingdays_v1,
    
    [Description("Schedules the message to be sent in the first available slot between 08:00 and 17:00 CET.")]
    Daytime_v1,
    
    [Description("Schedules the message to on, or as soon as possible after, the not-before time.")]
    Unrestricted_v1,
    
    [Description("Sends the message immediately (bypass scheduling). Only applicable for SMS, and typically only for SMS MFA-codes.")]
    TimeCritical_v1
    
}