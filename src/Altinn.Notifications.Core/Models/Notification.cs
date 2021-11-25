using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Notifications.Core.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int Priority { get; set; }

        public string Type { get; set; }

        public string Condition { get; set; }

        public DateTime SendTime { get; set; }

        public int Texts { get; set; }

        public string InstanceId { get; set; }

        public string PartyReference { get; set; }
    }
}
