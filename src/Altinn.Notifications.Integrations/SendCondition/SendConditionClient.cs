using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Integrations.SendCondition
{
    /// <summary>
    /// Implementation of <see cref="IConditionClient"/> using a maskinporten client with Digdir credentials
    /// </summary>
    public class SendConditionClient : IConditionClient
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="SendConditionClient"/> class.
        /// </summary>
        public SendConditionClient(HttpClient client)
        {
            _client = client;
        }

        /// <inheritdoc/>
        public async Task<Result<bool, ConditionClientError>> CheckSendCondition(Uri url)
        {
            HttpResponseMessage res = await _client.GetAsync(url);
            string responseString = await res.Content.ReadAsStringAsync();

            if (res.IsSuccessStatusCode)
            {
                try
                {
                    SendConditionResponse? conditionResponse = JsonSerializer.Deserialize<SendConditionResponse?>(responseString);

                    if (conditionResponse?.SendNotification != null)
                    {
                        return (bool)conditionResponse.SendNotification;
                    }
                    else
                    {
                        return new ConditionClientError { StatusCode = (int)res.StatusCode, Message = $"No condition response in body: {responseString}" };
                    }
                }
                catch (JsonException)
                {
                    return new ConditionClientError { Message = $"Deserialization into SendConditionResponse failed. Message {responseString}" };
                }
            }

            return new ConditionClientError { StatusCode = (int)res.StatusCode, Message = responseString };
        }
    }
}
