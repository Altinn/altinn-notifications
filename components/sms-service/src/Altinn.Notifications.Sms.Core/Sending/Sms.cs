using System.Text.Json;

namespace Altinn.Notifications.Sms.Core.Sending;

/// <summary>
/// Class representing an sms message
/// </summary>
public class Sms
{
    private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets or sets the id of the sms.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the sender of the sms message
    /// </summary>
    /// <remarks>
    /// Can be a literal string or a phone number
    /// </remarks>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient of the sms message
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contents of the sms message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class.
    /// </summary>
    public Sms(Guid notificationId, string sender, string recipient, string message)
    {
        NotificationId = notificationId;
        Recipient = recipient;
        Sender = sender;
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class.
    /// </summary>
    public Sms()
    {
    }

    /// <summary>
    /// Try to parse a json string into a<see cref="Sms"/>
    /// </summary>
    public static bool TryParse(string input, out Sms value)
    {
        Sms? parsedOutput;
        value = new Sms();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            parsedOutput = JsonSerializer.Deserialize<Sms>(input!, _serializerOptions);
            value = parsedOutput!;
            return value.NotificationId != Guid.Empty;
        }
        catch
        {
            // try parse, we simply return false if fails
        }

        return false;
    }
}
