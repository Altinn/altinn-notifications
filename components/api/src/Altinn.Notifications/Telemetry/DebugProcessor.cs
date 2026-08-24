using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Altinn.AccessManagement.Core.Models;
using AltinnCore.Authentication.Constants;
using Microsoft.Extensions.Primitives;
using OpenTelemetry;

namespace Altinn.Notifications.Telemetry
{
    /// <summary>
    /// Debug filter
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DebugProcessor"/> class.
    /// </remarks>
    public class DebugProcessor() : BaseProcessor<Activity>()
    {
        /// <summary>
        /// No action on end
        /// </summary>
        /// <param name="data">xx</param>
        public override void OnEnd(Activity data)
        {
            Console.WriteLine($"SPAN ENDED: {data.DisplayName} TraceId={data.TraceId}");
        }
    }
}
