namespace Altinn.Notifications.Core.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public DateTime SendTime { get; set; }

        public List<Message> Messages { get; set; } = new List<Message>();

        public List<Target> Targets { get; set; } = new List<Target>();

        public string? InstanceId { get; set; }

        public string? PartyReference { get; set; }

        public string? Sender { get; set; }
    }
}
