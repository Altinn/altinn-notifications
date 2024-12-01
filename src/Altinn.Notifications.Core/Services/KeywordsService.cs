using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Parties;
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
        public KeywordsService(IRegisterClient registerClient)
        {
            _registerClient = registerClient ?? throw new ArgumentNullException(nameof(registerClient));
        }

        /// <inheritdoc/>
        public bool ContainsRecipientNamePlaceholder(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNamePlaceholder);

        /// <inheritdoc/>
        public bool ContainsRecipientNumberPlaceholder(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Contains(_recipientNumberPlaceholder);

        /// <inheritdoc/>
        public async Task<IEnumerable<SmsRecipient>> ReplaceKeywordsAsync(IEnumerable<SmsRecipient> smsRecipients)
        {
            ArgumentNullException.ThrowIfNull(smsRecipients);

            var organizationNumbers = smsRecipients
                .Where(r => !string.IsNullOrWhiteSpace(r.OrganizationNumber))
                .Select(r => r.OrganizationNumber!)
                .ToList();

            var nationalIdentityNumbers = smsRecipients
                .Where(r => !string.IsNullOrWhiteSpace(r.NationalIdentityNumber))
                .Select(r => r.NationalIdentityNumber!)
                .ToList();

            var (personDetails, organizationDetails) = await FetchPartyDetailsAsync(organizationNumbers, nationalIdentityNumbers);

            foreach (var smsRecipient in smsRecipients)
            {
                smsRecipient.CustomizedBody =
                    ReplacePlaceholders(smsRecipient.CustomizedBody, smsRecipient.OrganizationNumber, smsRecipient.NationalIdentityNumber, organizationDetails, personDetails);
            }

            return smsRecipients;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<EmailRecipient>> ReplaceKeywordsAsync(IEnumerable<EmailRecipient> emailRecipients)
        {
            ArgumentNullException.ThrowIfNull(emailRecipients);

            var organizationNumbers = emailRecipients
                .Where(r => !string.IsNullOrWhiteSpace(r.OrganizationNumber))
                .Select(r => r.OrganizationNumber!)
                .ToList();

            var nationalIdentityNumbers = emailRecipients
                .Where(r => !string.IsNullOrWhiteSpace(r.NationalIdentityNumber))
                .Select(r => r.NationalIdentityNumber!)
                .ToList();

            var (personDetails, organizationDetails) = await FetchPartyDetailsAsync(organizationNumbers, nationalIdentityNumbers);

            foreach (var emailRecipient in emailRecipients)
            {
                emailRecipient.CustomizedBody =
                    ReplacePlaceholders(emailRecipient.CustomizedBody, emailRecipient.OrganizationNumber, emailRecipient.NationalIdentityNumber, organizationDetails, personDetails);

                emailRecipient.CustomizedSubject =
                    ReplacePlaceholders(emailRecipient.CustomizedSubject, emailRecipient.OrganizationNumber, emailRecipient.NationalIdentityNumber, organizationDetails, personDetails);
            }

            return emailRecipients;
        }

        /// <summary>
        /// Fetches party details for the given organization and national identity numbers.
        /// </summary>
        /// <param name="organizationNumbers">The list of organization numbers.</param>
        /// <param name="nationalIdentityNumbers">The list of national identity numbers.</param>
        /// <returns>A tuple containing lists of person and organization details.</returns>
        private async Task<(List<PartyDetails> PersonDetails, List<PartyDetails> OrganizationDetails)> FetchPartyDetailsAsync(
            List<string> organizationNumbers,
            List<string> nationalIdentityNumbers)
        {
            var organizationDetailsTask = organizationNumbers.Count != 0
                ? _registerClient.GetPartyDetailsForOrganizations(organizationNumbers)
                : Task.FromResult(new List<PartyDetails>());

            var personDetailsTask = nationalIdentityNumbers.Count != 0
                ? _registerClient.GetPartyDetailsForPersons(nationalIdentityNumbers)
                : Task.FromResult(new List<PartyDetails>());

            await Task.WhenAll(personDetailsTask, organizationDetailsTask);

            return (personDetailsTask.Result, organizationDetailsTask.Result);
        }

        /// <summary>
        /// Replaces placeholders in the given text with actual values from the provided details.
        /// </summary>
        /// <param name="customizedText">The text containing placeholders.</param>
        /// <param name="organizationNumber">The organization number.</param>
        /// <param name="nationalIdentityNumber">The national identity number.</param>
        /// <param name="organizationDetails">The list of organization details.</param>
        /// <param name="personDetails">The list of person details.</param>
        /// <returns>The text with placeholders replaced by actual values.</returns>
        private static string? ReplacePlaceholders(
            string? customizedText,
            string? organizationNumber,
            string? nationalIdentityNumber,
            IEnumerable<PartyDetails> organizationDetails,
            IEnumerable<PartyDetails> personDetails)
        {
            customizedText = ReplaceWithDetails(customizedText, organizationNumber, organizationDetails, p => p.OrganizationNumber);

            customizedText = ReplaceWithDetails(customizedText, nationalIdentityNumber, personDetails, p => p.NationalIdentityNumber);

            return customizedText;
        }

        /// <summary>
        /// Replaces placeholders in the given text with actual values from the provided details.
        /// </summary>
        /// <param name="customizedText">The text containing placeholders.</param>
        /// <param name="searchKey">The key to match in the details.</param>
        /// <param name="details">The list of details.</param>
        /// <param name="keySelector">The function to select the key from the details.</param>
        /// <returns>The text with placeholders replaced by actual values.</returns>
        private static string? ReplaceWithDetails(
            string? customizedText,
            string? searchKey,
            IEnumerable<PartyDetails> details,
            Func<PartyDetails, string?> keySelector)
        {
            if (string.IsNullOrWhiteSpace(searchKey))
            {
                return customizedText;
            }

            var detail = details.FirstOrDefault(e => keySelector(e) == searchKey);

            if (detail != null)
            {
                customizedText = customizedText?.Replace(_recipientNamePlaceholder, detail.Name ?? string.Empty);

                customizedText = customizedText?.Replace(_recipientNumberPlaceholder, keySelector(detail) ?? string.Empty);
            }

            return customizedText;
        }
    }
}
