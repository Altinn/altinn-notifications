using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface describing the service responding to service updates from Altinn components
    /// </summary>
    public interface IAltinnServiceUpdateService
    {
        /// <summary>
        /// Method for handling an update from a service
        /// </summary>
        public Task HandleServiceUpdate(string source, AltinnServiceUpdateSchema schema, string serializedData);
    }
}
