using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Integrations
{
    /// <summary>
    /// Interface describing a send condition client
    /// </summary>
    public interface IConditionClient
    {
        /// <summary>
        /// Sends a request to the provided url to check if the send condition is met
        /// </summary>
        /// <param name="url">The url to send the request to</param>
        /// <returns>
        /// A boolean with the send condition result or a <see cref="ConditionClientError"/>
        /// </returns>
        public Task<Result<bool, ConditionClientError>> CheckSendCondition(Uri url);
    }
}
