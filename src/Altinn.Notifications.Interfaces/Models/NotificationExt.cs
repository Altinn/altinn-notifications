
namespace Altinn.Notifications.Interfaces.Models
{
    public class NotificationExt
    {

        public string? InstanceId { get; set; }

        public List<TargetExt> Targets { get; set; } = new List<TargetExt>{ };

        public List<MessageExt> Messages { get; set; } = new List<MessageExt> { };

    }
}
