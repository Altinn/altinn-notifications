using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Email.Core.Status
{
    /// <summary>
    /// A class representing a send operation update object
    /// </summary>
    public class SendOperationResult
    {
        /// <summary>
        /// The notification id
        /// </summary>
        public Guid NotificationId { get; set; }

        /// <summary>
        /// The send operation id
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The email send result
        /// </summary>
        public EmailSendResult? SendResult { get; set; }

        /// <summary>
        /// Json serializes the <see cref="SendOperationResult"/>
        /// </summary>
        public string Serialize()
        {
            return JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                });
        }
    }
}
