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
        /// <summary>
        /// Checks whether the specified string contains the placeholder keyword <c>$recipientName$</c>.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><c>true</c> if the specified string contains the placeholder keyword <c>$recipientName$</c>; otherwise, <c>false</c>.</returns>
        public bool ContainsRecipientNamePlaceholder(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNamePlaceholder);
        }

        /// <inheritdoc/>
        /// <summary>
        /// Checks whether the specified string contains the placeholder keyword <c>$recipientNumber$</c>.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><c>true</c> if the specified string contains the placeholder keyword <c>$recipientNumber$</c>; otherwise, <c>false</c>.</returns>
        public bool ContainsRecipientNumberPlaceholder(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNumberPlaceholder);
        }

        /// <inheritdoc/>
        /// <summary>
        /// Replaces placeholder keywords in an <see cref="SmsRecipient"/> with actual values.
        /// </summary>
        /// <param name="smsRecipient">The <see cref="SmsRecipient"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="SmsRecipient"/> with the placeholder keywords replaced by actual values.</returns>
        public async Task<SmsRecipient> ReplaceKeywordsAsync(SmsRecipient smsRecipient)
        {
            ArgumentNullException.ThrowIfNull(smsRecipient);

            smsRecipient = await ReplaceKeywordsAsync(smsRecipient, r => r.CustomizedBody, (r, v) => r.CustomizedBody = v, r => r.NationalIdentityNumber, r => r.OrganizationNumber);

            return smsRecipient;
        }

        /// <inheritdoc/>
        /// <summary>
        /// Replaces placeholder keywords in an <see cref="EmailRecipient"/> with actual values.
        /// </summary>
        /// <param name="emailRecipient">The <see cref="EmailRecipient"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="EmailRecipient"/> with the placeholder keywords replaced by actual values.</returns>
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
        private async Task<T> ReplaceKeywordsAsync<T>(
            T recipient,
            Func<T, string?> getBody,
            Action<T, string?> setBody,
            Func<T, string?> nationalIdentityNumberGetter,
            Func<T, string?> organizationNumberGetter)
        {
            if (ContainsRecipientNamePlaceholder(getBody(recipient)))
            {
                await ReplaceRecipientNamePlaceholderAsync(recipient, getBody, setBody, nationalIdentityNumberGetter, organizationNumberGetter);
            }

            if (ContainsRecipientNumberPlaceholder(getBody(recipient)))
            {
                ReplaceRecipientNumberPlaceholder(recipient, getBody, setBody, nationalIdentityNumberGetter, organizationNumberGetter);
            }

            return recipient;
        }

        /// <summary>
        /// Replaces the recipient name placeholder with the actual recipient name.
        /// </summary>
        /// <typeparam name="T">The type of the recipient.</typeparam>
        /// <param name="recipient">The recipient to process.</param>
        /// <param name="getBody">A function to get the body of the recipient.</param>
        /// <param name="setBody">A function to set the body of the recipient.</param>
        /// <param name="nationalIdentityNumberGetter">A function to get the national identity number of the recipient.</param>
        /// <param name="organizationNumberGetter">A function to get the organization number of the recipient.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ReplaceRecipientNamePlaceholderAsync<T>(
            T recipient,
            Func<T, string?> getBody,
            Action<T, string?> setBody,
            Func<T, string?> nationalIdentityNumberGetter,
            Func<T, string?> organizationNumberGetter)
        {
            var nationalIdentityNumber = nationalIdentityNumberGetter(recipient);
            if (!string.IsNullOrWhiteSpace(nationalIdentityNumber))
            {
                var partyDetails = await _registerClient.GetPartyDetailsForPersons(new List<string> { nationalIdentityNumber });
                if (partyDetails != null && partyDetails.Count > 0)
                {
                    setBody(recipient, getBody(recipient)?.Replace(_recipientNamePlaceholder, partyDetails[0]?.Name ?? string.Empty));
                }
            }

            var organizationNumber = organizationNumberGetter(recipient);
            if (!string.IsNullOrWhiteSpace(organizationNumber))
            {
                var partyDetails = await _registerClient.GetPartyDetailsForOrganizations(new List<string> { organizationNumber });
                if (partyDetails != null && partyDetails.Count > 0)
                {
                    setBody(recipient, getBody(recipient)?.Replace(_recipientNamePlaceholder, partyDetails[0]?.Name ?? string.Empty));
                }
            }
        }

        /// <summary>
        /// Replaces the recipient number placeholder with the actual recipient number.
        /// </summary>
        /// <typeparam name="T">The type of the recipient.</typeparam>
        /// <param name="recipient">The recipient to process.</param>
        /// <param name="getBody">A function to get the body of the recipient.</param>
        /// <param name="setBody">A function to set the body of the recipient.</param>
        /// <param name="nationalIdentityNumberGetter">A function to get the national identity number of the recipient.</param>
        /// <param name="organizationNumberGetter">A function to get the organization number of the recipient.</param>
        private void ReplaceRecipientNumberPlaceholder<T>(
            T recipient,
            Func<T, string?> getBody,
            Action<T, string?> setBody,
            Func<T, string?> nationalIdentityNumberGetter,
            Func<T, string?> organizationNumberGetter)
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
    }
}
