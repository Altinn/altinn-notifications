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

            var personDetailsTask = nationalIdentityNumbers.Count > 0
                ? _registerClient.GetPartyDetailsForPersons(nationalIdentityNumbers)
                : Task.FromResult(new List<PartyDetails>());

            var organizationDetailsTask = organizationNumbers.Count > 0
                ? _registerClient.GetPartyDetailsForOrganizations(organizationNumbers)
                : Task.FromResult(new List<PartyDetails>());

            await Task.WhenAll(personDetailsTask, organizationDetailsTask);

            var personDetails = personDetailsTask.Result;
            var organizationDetails = organizationDetailsTask.Result;

            foreach (var smsRecipient in smsRecipients)
            {
                smsRecipient.CustomizedBody = ReplaceRecipientPlaceholders(smsRecipient.CustomizedBody, smsRecipient.OrganizationNumber, smsRecipient.NationalIdentityNumber, organizationDetails, personDetails);
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

            var personDetailsTask = nationalIdentityNumbers.Count > 0
                ? _registerClient.GetPartyDetailsForPersons(nationalIdentityNumbers)
                : Task.FromResult(new List<PartyDetails>());

            var organizationDetailsTask = organizationNumbers.Count > 0
                ? _registerClient.GetPartyDetailsForOrganizations(organizationNumbers)
                : Task.FromResult(new List<PartyDetails>());

            await Task.WhenAll(personDetailsTask, organizationDetailsTask);

            var personDetails = personDetailsTask.Result;
            var organizationDetails = organizationDetailsTask.Result;

            foreach (var emailRecipient in emailRecipients)
            {
                emailRecipient.CustomizedBody = ReplaceRecipientPlaceholders(emailRecipient.CustomizedBody, emailRecipient.OrganizationNumber, emailRecipient.NationalIdentityNumber, organizationDetails, personDetails);

                emailRecipient.CustomizedSubject = ReplaceRecipientPlaceholders(emailRecipient.CustomizedSubject, emailRecipient.OrganizationNumber, emailRecipient.NationalIdentityNumber, organizationDetails, personDetails);
            }

            return emailRecipients;
        }

        /// <summary>
        /// Replaces the recipient placeholders in the specified text with actual values.
        /// </summary>
        /// <param name="text">The text to process.</param>
        /// <param name="organizationNumber">The organization number of the recipient.</param>
        /// <param name="nationalIdentityNumber">The national identity number of the recipient.</param>
        /// <param name="organizationDetails">The list of organization details.</param>
        /// <param name="personDetails">The list of person details.</param>
        /// <returns>The text with the placeholders replaced by actual values.</returns>
        private static string? ReplaceRecipientPlaceholders(string? text, string? organizationNumber, string? nationalIdentityNumber, List<PartyDetails> organizationDetails, List<PartyDetails> personDetails)
        {
            if (!string.IsNullOrWhiteSpace(organizationNumber))
            {
                var partyDetail = organizationDetails.Find(p => p.OrganizationNumber == organizationNumber);
                if (partyDetail != null)
                {
                    text = text?.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
                }
            }

            if (!string.IsNullOrWhiteSpace(nationalIdentityNumber))
            {
                var partyDetail = personDetails.Find(p => p.NationalIdentityNumber == nationalIdentityNumber);
                if (partyDetail != null)
                {
                    text = text?.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
                }
            }

            return text;
        }
    }
}
