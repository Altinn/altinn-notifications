#nullable enable

using System.Net.Http.Headers;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Dialogporten;

/// <summary>
/// Represents a client for interacting with the Dialogporten service.
/// </summary>
public class DialogportenClient : IDialogportenClient
{
    private readonly HttpClient _client;
    private readonly DialogportenSettings _dialogportenSettings;
    private readonly IAuthenticationContext _authenticationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogportenClient"/> class.
    /// </summary>
    public DialogportenClient(
        HttpClient client, 
        IOptions<DialogportenSettings> dialogportenSettings, 
        IAuthenticationContext authenticationContext)
    {
        _client = client;
        _client.BaseAddress = new Uri(dialogportenSettings.Value.BaseUrl);

        _dialogportenSettings = dialogportenSettings.Value;
        _authenticationContext = authenticationContext;
    }

    /// <summary>
    /// Checks if the current user has access to a specific dialog identified by its ID.
    /// </summary>
    /// <remarks>
    /// This method performs a call to the Dialogporten service with the user's credentials to determine 
    /// if they have access to the specified dialog. The method doesn't care about the response body, but
    /// will determine user access based on the response status code.
    /// </remarks>
    /// <param name="dialogId">The ID of the dialog to check access for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a boolean
    /// indicating whether the user has access to the dialog. Also returns false if the Dialogporten 
    /// integration is disabled or if the user is not authenticated.
    /// </returns>
    public async Task<bool> CheckUserAccessToDialog(Guid dialogId, CancellationToken cancellationToken)
    {
        if (!_dialogportenSettings.Enabled)
        {
            return false;
        }

        string? token = _authenticationContext.GetTokenFromContext();
        
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(
            $"enduser/dialoglookup?instanceRef=urn:altinn:dialog-id:{dialogId}", cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
