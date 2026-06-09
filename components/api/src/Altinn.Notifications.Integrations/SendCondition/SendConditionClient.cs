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
            try
            {
                using var response = await _client.GetAsync(url);
                return await ProcessHttpResponse(response);
            }
            catch (HttpRequestException ex)
            {
                return new ConditionClientError { Message = $"HTTP request failed: {ex.Message}" };
            }
            catch (TaskCanceledException ex)
            {
                return new ConditionClientError { Message = $"Request timed out: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new ConditionClientError { Message = $"Unexpected error during HTTP request: {ex.Message}" };
            }
        }

        /// <summary>
        /// Deserializes the response string into a SendConditionResponse object
        /// </summary>
        /// <param name="responseString">The response string to deserialize</param>
        /// <param name="statusCode">The HTTP status code</param>
        /// <returns>A boolean with the send condition result or a <see cref="ConditionClientError"/></returns>
        private static Result<bool, ConditionClientError> DeserializeResponse(string responseString, int statusCode)
        {
            try
            {
                SendConditionResponse? conditionResponse = JsonSerializer.Deserialize<SendConditionResponse?>(responseString);

                if (conditionResponse?.SendNotification != null)
                {
                    return conditionResponse.SendNotification.Value;
                }

                return new ConditionClientError
                {
                    StatusCode = statusCode,
                    Message = $"No condition response in the body: {(responseString.Length > 50 ? responseString[..50] + "..." : responseString)}"
                };
            }
            catch (JsonException ex)
            {
                return new ConditionClientError
                {
                    Message = $"Deserialization into SendConditionResponse failed. Error: {ex.Message}, Response: {(responseString.Length > 50 ? responseString[..50] + "..." : responseString)}"
                };
            }
        }

        /// <summary>
        /// Processes the HTTP response and extracts the send condition result.
        /// </summary>
        /// <param name="response">The HTTP response to process</param>
        /// <returns>A boolean with the send condition result or a <see cref="ConditionClientError"/></returns>
        private static async Task<Result<bool, ConditionClientError>> ProcessHttpResponse(HttpResponseMessage response)
        {
            string responseString;

            try
            {
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return new ConditionClientError
                {
                    StatusCode = Convert.ToInt32(response.StatusCode),
                    Message = $"Unexpected response content: {ex.Message}"
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ConditionClientError
                {
                    StatusCode = Convert.ToInt32(response.StatusCode),
                    Message = $"Unsuccessful response. First 50 character received: {(responseString.Length > 50 ? responseString[..50] + "..." : responseString)}"
                };
            }

            if (string.IsNullOrWhiteSpace(responseString))
            {
                return new ConditionClientError
                {
                    Message = "Response body is empty",
                    StatusCode = Convert.ToInt32(response.StatusCode),
                };
            }

            return DeserializeResponse(responseString, (int)response.StatusCode);
        }
    }
}
