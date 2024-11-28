using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Provides methods for handling keyword placeholders in collections of <see cref="Sms"/> or <see cref="Email"/>.
    /// </summary>
    public class KeywordsService : IKeywordsService
    {
        private readonly IRegisterClient _registerClient;

        private const string _recipientNamePlaceholder = "$recipientName$";
        private const string _recipientNumberPlaceholder = "$recipientNumber$";

        private static readonly Lazy<Regex> _recipientNamePlaceholderRegex =
            new(() => new Regex(Regex.Escape(_recipientNamePlaceholder), RegexOptions.Compiled | RegexOptions.CultureInvariant));

        private static readonly Lazy<Regex> _recipientNumberPlaceholderRegex =
            new(() => new Regex(Regex.Escape(_recipientNumberPlaceholder), RegexOptions.Compiled | RegexOptions.CultureInvariant));

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
            return !string.IsNullOrWhiteSpace(value) && _recipientNamePlaceholderRegex.Value.IsMatch(value);
        }

        /// <inheritdoc/>
        public bool ContainsRecipientNumberPlaceholder(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && _recipientNumberPlaceholderRegex.Value.IsMatch(value);
        }

        /// <inheritdoc/>
        public async Task<List<Sms>> ReplaceKeywordsAsync(List<Sms> smsList)
        {
            ArgumentNullException.ThrowIfNull(smsList);

            if (smsList.Count == 0)
            {
                return smsList;
            }

            smsList = await InjectPersonNameAsync(smsList);
            smsList = InjectNationalIdentityNumbers(smsList);

            smsList = InjectOrganizationNumbers(smsList);
            smsList = await InjectOrganizationNameAsync(smsList);

            return smsList;
        }

        /// <inheritdoc/>
        public async Task<EmailRecipient> ReplaceKeywordsAsync(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            emailRecipient = await InjectPersonNameAsync(emailRecipient);
            emailRecipient = InjectNationalIdentityNumbers(emailRecipient);

            emailRecipient = InjectOrganizationNumbers(emailRecipient);
            emailRecipient = await InjectOrganizationNameAsync(emailRecipient);

            return emailRecipient;
        }

        /// <summary>
        /// Injects the recipient's name into the SMS where the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="smsList">The list of <see cref="Sms"/>.</param>
        /// <returns>The updated list of <see cref="Sms"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="smsList"/> is null.</exception>
        private async Task<List<Sms>> InjectPersonNameAsync(List<Sms> smsList)
        {
            ArgumentNullException.ThrowIfNull(smsList);

            if (smsList.Count == 0)
            {
                return smsList;
            }

            var nationalIdentityNumbers = smsList
                .Where(e => ContainsRecipientNamePlaceholder(e.Message))
                .Where(e => !string.IsNullOrEmpty(e.NationalIdentityNumber))
                .Select(e => e.NationalIdentityNumber)
                .Distinct()
                .ToList();

            if (nationalIdentityNumbers.Count == 0)
            {
                return smsList;
            }

            var partyDetails = await _registerClient.GetPartyDetailsForPersons(nationalIdentityNumbers);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return smsList;
            }

            foreach (var partyDetail in partyDetails)
            {
                var sms = smsList.Find(e => e.NationalIdentityNumber == partyDetail.NationalIdentityNumber);
                if (sms == null)
                {
                    continue;
                }

                sms.Message = sms.Message.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
            }

            return smsList;
        }

        /// <summary>
        /// Injects the recipient's name wherever the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="emailRecipient">The list of <see cref="EmailRecipient"/>.</param>
        /// <returns>The updated list of <see cref="EmailRecipient"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailRecipient"/> is null.</exception>
        private async Task<EmailRecipient> InjectPersonNameAsync(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            // If the recipient does not contain the recipient name placeholder, we do not need to look up the person name.
            bool containsRecipientNamePlaceholder = ContainsRecipientNamePlaceholder(emailRecipient.CustomizedBody) ||
                                                    ContainsRecipientNamePlaceholder(emailRecipient.CustomizedSubject);
            if (!containsRecipientNamePlaceholder)
            {
                return emailRecipient;
            }

            // If the recipient does not have an person number, we do not need to look up the person name.
            if (string.IsNullOrWhiteSpace(emailRecipient.NationalIdentityNumber))
            {
                return emailRecipient;
            }

            // Look up the person name and replace the recipient name placeholder with the person name.
            var partyDetails = await _registerClient.GetPartyDetailsForOrganizations([emailRecipient.NationalIdentityNumber]);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return emailRecipient;
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedBody))
            {
                emailRecipient.CustomizedBody = emailRecipient.CustomizedBody.Replace(_recipientNamePlaceholder, partyDetails[0].Name ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedSubject))
            {
                emailRecipient.CustomizedSubject = emailRecipient.CustomizedSubject.Replace(_recipientNamePlaceholder, partyDetails[0].Name ?? string.Empty);
            }

            return emailRecipient;
        }

        /// <summary>
        /// Injects the recipient's national identity number into the SMS where the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="smsList">The list of <see cref="Sms"/>.</param>
        /// <returns>The updated list of <see cref="Sms"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="smsList"/> is null.</exception>
        private List<Sms> InjectNationalIdentityNumbers(List<Sms> smsList)
        {
            ArgumentNullException.ThrowIfNull(smsList);

            if (smsList.Count == 0)
            {
                return smsList;
            }

            var smsWithNationalIdentityNumber = smsList
                .Where(e => ContainsRecipientNumberPlaceholder(e.Message))
                .Where(e => !string.IsNullOrEmpty(e.NationalIdentityNumber))
                .Distinct()
                .ToList();

            foreach (var smsWithKeyword in smsWithNationalIdentityNumber)
            {
                var sms = smsList.Find(e => e.NationalIdentityNumber == smsWithKeyword.NationalIdentityNumber);
                if (sms == null)
                {
                    continue;
                }

                sms.Message = sms.Message.Replace(_recipientNumberPlaceholder, sms.NationalIdentityNumber ?? string.Empty);
            }

            return smsList;
        }

        /// <summary>
        /// Injects the recipient's national identity number wherever the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="emailRecipient">The list of <see cref="EmailRecipient"/>.</param>
        /// <returns>The updated list of <see cref="EmailRecipient"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailRecipient"/> is null.</exception>
        private EmailRecipient InjectNationalIdentityNumbers(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            bool containsRecipientNumberPlaceholder = ContainsRecipientNumberPlaceholder(emailRecipient.CustomizedBody) ||
                                                      ContainsRecipientNumberPlaceholder(emailRecipient.CustomizedSubject);
            if (!containsRecipientNumberPlaceholder)
            {
                return emailRecipient;
            }

            if (string.IsNullOrWhiteSpace(emailRecipient.NationalIdentityNumber))
            {
                return emailRecipient;
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedBody))
            {
                emailRecipient.CustomizedBody = emailRecipient.CustomizedBody.Replace(_recipientNumberPlaceholder, emailRecipient.NationalIdentityNumber ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedSubject))
            {
                emailRecipient.CustomizedSubject = emailRecipient.CustomizedSubject.Replace(_recipientNumberPlaceholder, emailRecipient.NationalIdentityNumber ?? string.Empty);
            }

            return emailRecipient;
        }

        /// <summary>
        /// Injects the recipient's organization number into the SMS where the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="smsList">The list of <see cref="Sms"/>.</param>
        /// <returns>The updated list of <see cref="Sms"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="smsList"/> is null.</exception>
        private List<Sms> InjectOrganizationNumbers(List<Sms> smsList)
        {
            ArgumentNullException.ThrowIfNull(smsList);

            if (smsList.Count == 0)
            {
                return smsList;
            }

            var smsWithNationalIdentityNumber = smsList
                .Where(e => ContainsRecipientNumberPlaceholder(e.Message))
                .Where(e => !string.IsNullOrEmpty(e.OrganizationNumber))
                .Distinct()
                .ToList();

            foreach (var smsWithKeyword in smsWithNationalIdentityNumber)
            {
                var sms = smsList.Find(e => e.OrganizationNumber == smsWithKeyword.OrganizationNumber);
                if (sms == null)
                {
                    continue;
                }

                sms.Message = sms.Message.Replace(_recipientNumberPlaceholder, sms.OrganizationNumber ?? string.Empty);
            }

            return smsList;
        }

        /// <summary>
        /// Injects the recipient's organization number wherever the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="emailRecipient">The list of <see cref="EmailRecipient"/>.</param>
        /// <returns>The updated list of <see cref="EmailRecipient"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailRecipient"/> is null.</exception>
        private EmailRecipient InjectOrganizationNumbers(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            bool containsRecipientNumberPlaceholder = ContainsRecipientNumberPlaceholder(emailRecipient.CustomizedBody) ||
                                                      ContainsRecipientNumberPlaceholder(emailRecipient.CustomizedSubject);
            if (!containsRecipientNumberPlaceholder)
            {
                return emailRecipient;
            }

            if (string.IsNullOrWhiteSpace(emailRecipient.OrganizationNumber))
            {
                return emailRecipient;
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedBody))
            {
                emailRecipient.CustomizedBody = emailRecipient.CustomizedBody.Replace(_recipientNumberPlaceholder, emailRecipient.OrganizationNumber ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedSubject))
            {
                emailRecipient.CustomizedSubject = emailRecipient.CustomizedSubject.Replace(_recipientNumberPlaceholder, emailRecipient.OrganizationNumber ?? string.Empty);
            }

            return emailRecipient;
        }

        /// <summary>
        /// Injects the recipient's organization name into the SMS where the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="smsList">The list of <see cref="Sms"/>.</param>
        /// <returns>The updated list of <see cref="Sms"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="smsList"/> is null.</exception>
        private async Task<List<Sms>> InjectOrganizationNameAsync(List<Sms> smsList)
        {
            ArgumentNullException.ThrowIfNull(smsList);

            if (smsList.Count == 0)
            {
                return smsList;
            }

            var organizationNumbers = smsList
                .Where(e => ContainsRecipientNamePlaceholder(e.Message))
                .Where(e => !string.IsNullOrEmpty(e.OrganizationNumber))
                .Select(e => e.OrganizationNumber)
                .Distinct()
                .ToList();

            if (organizationNumbers.Count == 0)
            {
                return smsList;
            }

            var partyDetails = await _registerClient.GetPartyDetailsForOrganizations(organizationNumbers);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return smsList;
            }

            foreach (var partyDetail in partyDetails)
            {
                var sms = smsList.Find(e => e.OrganizationNumber == partyDetail.OrganizationNumber);
                if (sms == null)
                {
                    continue;
                }

                sms.Message = sms.Message.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
            }

            return smsList;
        }

        /// <summary>
        /// Injects the recipient's organization name wherever the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="emailRecipient">The <see cref="EmailRecipient"/>.</param>
        /// <returns>The updated <see cref="EmailRecipient"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailRecipient"/> is null.</exception>
        private async Task<EmailRecipient> InjectOrganizationNameAsync(EmailRecipient emailRecipient)
        {
            ArgumentNullException.ThrowIfNull(emailRecipient);

            // If the recipient does not contain the recipient name placeholder, we do not need to look up the organization name.
            bool containsRecipientNamePlaceholder = ContainsRecipientNamePlaceholder(emailRecipient.CustomizedBody) ||
                                                    ContainsRecipientNamePlaceholder(emailRecipient.CustomizedSubject);
            if (!containsRecipientNamePlaceholder)
            {
                return emailRecipient;
            }

            // If the recipient does not have an organization number, we do not need to look up the organization name.
            if (string.IsNullOrWhiteSpace(emailRecipient.OrganizationNumber))
            {
                return emailRecipient;
            }

            // Look up the organization name and replace the recipient name placeholder with the organization name.
            var partyDetails = await _registerClient.GetPartyDetailsForOrganizations([emailRecipient.OrganizationNumber]);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return emailRecipient;
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedBody))
            {
                emailRecipient.CustomizedBody = emailRecipient.CustomizedBody.Replace(_recipientNamePlaceholder, partyDetails[0].Name ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(emailRecipient.CustomizedSubject))
            {
                emailRecipient.CustomizedSubject = emailRecipient.CustomizedSubject.Replace(_recipientNamePlaceholder, partyDetails[0].Name ?? string.Empty);
            }

            return emailRecipient;
        }
    }
}
