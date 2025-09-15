using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;
using Altinn.Notifications.Sms.Core.Status;

using Microsoft.Extensions.Logging;

using LinkMobilityModel = global::LinkMobility.PSWin.Client.Model;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Represents an implementation of <see cref="ISmsClient"/> that sends text messages using LinkMobility's SMS gateway.
/// </summary>
public class SmsClient : ISmsClient
{
    private const int DefaultTimeToLiveInHours = 48;
    private readonly IAltinnGatewayClient _client;
    private readonly ILogger<SmsClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsClient"/> class.
    /// </summary>
    public SmsClient(IAltinnGatewayClient client, ILogger<SmsClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms)
    {
        var linkMobilitySms = CreateLinkMobilitySms(sms, TimeSpan.FromHours(DefaultTimeToLiveInHours));

        return await SendToLinkMobilityAsync(linkMobilitySms);
    }

    /// <inheritdoc />
    public async Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms, int timeToLiveInSeconds)
    {
        var linkMobilitySms = CreateLinkMobilitySms(sms, TimeSpan.FromSeconds(timeToLiveInSeconds));

        return await SendToLinkMobilityAsync(linkMobilitySms);
    }

    /// <summary>
    /// Creates a <see cref="LinkMobilityModel.Sms"/> model from the core SMS and a time-to-live value.
    /// </summary>
    /// <param name="sms">The core SMS message containing message, recipient, sender, and notification ID.</param>
    /// <param name="timeToLive">The time-to-live for the SMS message.</param>
    /// <returns>A configured <see cref="LinkMobilityModel.Sms"/> instance.</returns>
    private static LinkMobilityModel.Sms CreateLinkMobilitySms(Core.Sending.Sms sms, TimeSpan timeToLive)
    {
        return new LinkMobilityModel.Sms(sms.Sender, sms.Recipient, sms.Message)
        {
            TimeToLive = timeToLive
        };
    }

    /// <summary>
    /// Sends the SMS message using the LinkMobility gateway and handles the result.
    /// </summary>
    /// <param name="linkMobilitySms">The LinkMobility SMS message to send.</param>
    /// <returns>
    /// A <see cref="Result{T, TError}"/> that represents the outcome of the send operation:
    /// <list type="table">
    ///   <item>On success: a unique string identifier for tracking the message.</item>
    ///   <item>On failure: a <see cref="SmsClientErrorResponse"/> providing details about the error.</item>
    /// </list>
    /// </returns>
    private async Task<Result<string, SmsClientErrorResponse>> SendToLinkMobilityAsync(LinkMobilityModel.Sms linkMobilitySms)
    {
        var result = await _client.SendAsync(linkMobilitySms);

        if (result.IsStatusOk)
        {
            return result.GatewayReference;
        }

        if (result.StatusText.StartsWith("Invalid RCV"))
        {
            return new SmsClientErrorResponse
            {
                ErrorMessage = result.StatusText,
                SendResult = SmsSendResult.Failed_InvalidRecipient
            };
        }

        _logger.LogWarning("// SmsClient // SendAsync // Failed to send SMS. Status: {StatusText}", result.StatusText);

        return new SmsClientErrorResponse
        {
            SendResult = SmsSendResult.Failed
        };
    }
}
