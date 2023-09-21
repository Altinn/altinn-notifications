using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Class representing an email
/// </summary>
public class Email
{
    /// <summary>
    /// Gets or sets the id of the email.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body of the email.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the to fromAdress of the email.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the to adress of the email.
    /// </summary>
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the email.
    /// </summary>
    public EmailContentType ContentType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Email"/> class.
    /// </summary>
    public Email(Guid notificationId, string subject, string body, string fromAddress, string toAddress, EmailContentType contentType)
    {
        NotificationId = notificationId;
        Subject = subject;
        Body = body;
        FromAddress = fromAddress;
        ToAddress = toAddress;
        ContentType = contentType;
    }

    private Email()
    {
    }

    /// <summary>
    /// Try to parse a json string into a<see cref="Email"/>
    /// </summary>
    public static bool TryParse(string input, out Email value)
    {
        Email? parsedOutput;
        value = new Email();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            parsedOutput = JsonSerializer.Deserialize<Email>(
            input!,
            new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });

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
