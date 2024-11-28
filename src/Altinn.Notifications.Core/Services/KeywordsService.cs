using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Provides methods for handling keyword placeholders in <see cref="SmsRecipient"/> and <see cref="EmailRecipient"/>.
    /// </summary>
    public class KeywordsService : IKeywordsService
    {
        private readonly IRegisterClient _registerClient;

        private const string _recipientNamePlaceholder = "$recipientName$";
        private const string _recipientNumberPlaceholder = "$recipientNumber$";

        /// <summary>
        /// Initializes a new instance of the <see cref="KeywordsService"/> class.
        /// </summary>
        /// <param name="registerClient">The register client to interact with the register service.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="registerClient"/> is null.</exception>
        public KeywordsService(IRegisterClient registerClient)
        {
            _registerClient = registerClient ?? throw new ArgumentNullException(nameof(registerClient));
        }

        /// <inheritdoc/>
        public bool ContainsRecipientNamePlaceholder(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNamePlaceholder);
        }

        /// <inheritdoc/>
        public bool ContainsRecipientNumberPlaceholder(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNumberPlaceholder);
        }

        /// <inheritdoc/>
        public async Task<SmsRecipient> ReplaceKeywordsAsync(SmsRecipient smsRecipient)
        {
            ArgumentNullException.ThrowIfNull(smsRecipient);

            smsRecipient = await ReplaceKeywordsAsync(smsRecipient, r => r.CustomizedBody, (r, v) => r.CustomizedBody = v, r => r.NationalIdentityNumber, r => r.OrganizationNumber);

            return smsRecipient;
        }

        /// <inheritdoc/>
        public async Task<EmailRecipient> ReplaceKeywordsAsync(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            emailRecipient = await ReplaceKeywordsAsync(emailRecipient, r => r.CustomizedBody, (r, v) => r.CustomizedBody = v, r => r.NationalIdentityNumber, r => r.OrganizationNumber);
            emailRecipient = await ReplaceKeywordsAsync(emailRecipient, r => r.CustomizedSubject, (r, v) => r.CustomizedSubject = v, r => r.NationalIdentityNumber, r => r.OrganizationNumber);

            return emailRecipient;
        }

        /// <summary>
        /// Replaces placeholder keywords with actual values.
        /// </summary>
        /// <typeparam name="T">The type of the recipient.</typeparam>
        /// <param name="recipient">The recipient to process.</param>
        /// <param name="getBody">A function to get the body of the recipient.</param>
        /// <param name="setBody">A function to set the body of the recipient.</param>
        /// <param name="nationalIdentityNumberGetter">A function to get the national identity number of the recipient.</param>
        /// <param name="organizationNumberGetter">A function to get the organization number of the recipient.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the processed recipient.</returns>
        private async Task<T> ReplaceKeywordsAsync<T>(T recipient, Func<T, string?> getBody, Action<T, string?> setBody, Func<T, string?> nationalIdentityNumberGetter, Func<T, string?> organizationNumberGetter)
        {
            if (ContainsRecipientNamePlaceholder(getBody(recipient)))
            {
                var nationalIdentityNumber = nationalIdentityNumberGetter(recipient);
                if (!string.IsNullOrWhiteSpace(nationalIdentityNumber))
                {
                    var partyDetails = await _registerClient.GetPartyDetailsForPersons([nationalIdentityNumber]);
                    if (partyDetails != null && partyDetails.Count > 0)
                    {
                        setBody(recipient, getBody(recipient)?.Replace(_recipientNamePlaceholder, partyDetails[0]?.Name ?? string.Empty));
                    }
                }

                var organizationNumber = organizationNumberGetter(recipient);
                if (!string.IsNullOrWhiteSpace(organizationNumber))
                {
                    var partyDetails = await _registerClient.GetPartyDetailsForOrganizations([organizationNumber]);
                    if (partyDetails != null && partyDetails.Count > 0)
                    {
                        setBody(recipient, getBody(recipient)?.Replace(_recipientNamePlaceholder, partyDetails[0]?.Name ?? string.Empty));
                    }
                }
            }

            if (ContainsRecipientNumberPlaceholder(getBody(recipient)))
            {
                var nationalIdentityNumber = nationalIdentityNumberGetter(recipient);
                if (!string.IsNullOrWhiteSpace(nationalIdentityNumber))
                {
                    setBody(recipient, getBody(recipient)?.Replace(_recipientNumberPlaceholder, nationalIdentityNumber));
                }

                var organizationNumber = organizationNumberGetter(recipient);
                if (!string.IsNullOrWhiteSpace(organizationNumber))
                {
                    setBody(recipient, getBody(recipient)?.Replace(_recipientNumberPlaceholder, organizationNumber));
                }
            }

            return recipient;
        }
    }
}
