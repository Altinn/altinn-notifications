namespace Altinn.Notifications.Core.Models
{
    public class Message
    {
        public string? EmailSubject { get; set; }

        public string? EmailBody { get; set; }

        public string? SmsText { get; set; }

        public string Language { get; set; } = "nb";
    }
}