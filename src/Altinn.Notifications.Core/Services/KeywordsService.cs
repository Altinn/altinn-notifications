using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
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
        public bool ContainsRecipientNamePlaceholder(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && _recipientNamePlaceholderRegex.Value.IsMatch(value);
        }

        /// <inheritdoc/>
        public bool ContainsRecipientNumberPlaceholder(string value)
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
        public async Task<List<Email>> ReplaceKeywordsAsync(List<Email> emailList)
        {
            ArgumentNullException.ThrowIfNull(emailList);

            if (emailList.Count == 0)
            {
                return emailList;
            }

            emailList = await InjectPersonNameAsync(emailList);
            emailList = InjectNationalIdentityNumbers(emailList);

            emailList = InjectOrganizationNumbers(emailList);
            emailList = await InjectOrganizationNameAsync(emailList);

            return emailList;
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
        /// Injects the recipient's name into the email where the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="emailList">The list of <see cref="Email"/>.</param>
        /// <returns>The updated list of <see cref="Email"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailList"/> is null.</exception>
        private async Task<List<Email>> InjectPersonNameAsync(List<Email> emailList)
        {
            ArgumentNullException.ThrowIfNull(emailList);

            if (emailList.Count == 0)
            {
                return emailList;
            }

            var nationalIdentityNumbers = emailList
                .Where(e => ContainsRecipientNamePlaceholder(e.Subject) || ContainsRecipientNamePlaceholder(e.Body))
                .Where(e => !string.IsNullOrEmpty(e.NationalIdentityNumber))
                .Select(e => e.NationalIdentityNumber)
                .Distinct()
                .ToList();

            if (nationalIdentityNumbers.Count == 0)
            {
                return emailList;
            }

            var partyDetails = await _registerClient.GetPartyDetailsForPersons(nationalIdentityNumbers);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return emailList;
            }

            foreach (var partyDetail in partyDetails)
            {
                var email = emailList.Find(e => e.NationalIdentityNumber == partyDetail.NationalIdentityNumber);
                if (email == null)
                {
                    continue;
                }

                email.Body = email.Body.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
                email.Subject = email.Subject.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
            }

            return emailList;
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
        /// Injects the recipient's national identity number into the email where the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="emailList">The list of <see cref="Email"/>.</param>
        /// <returns>The updated list of <see cref="Email"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailList"/> is null.</exception>
        private List<Email> InjectNationalIdentityNumbers(List<Email> emailList)
        {
            ArgumentNullException.ThrowIfNull(emailList);

            if (emailList.Count == 0)
            {
                return emailList;
            }

            var emailWithNationalIdentityNumber = emailList
                .Where(e => ContainsRecipientNumberPlaceholder(e.Subject) || ContainsRecipientNumberPlaceholder(e.Body))
                .Where(e => !string.IsNullOrEmpty(e.NationalIdentityNumber))
                .Distinct()
                .ToList();

            foreach (var emailWithKeyword in emailWithNationalIdentityNumber)
            {
                var email = emailList.Find(e => e.NationalIdentityNumber == emailWithKeyword.NationalIdentityNumber);
                if (email == null)
                {
                    continue;
                }

                email.Body = email.Body.Replace(_recipientNumberPlaceholder, email.NationalIdentityNumber ?? string.Empty);
                email.Subject = email.Subject.Replace(_recipientNumberPlaceholder, email.NationalIdentityNumber ?? string.Empty);
            }

            return emailList;
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
        /// Injects the recipient's organization number into the email where the $recipientNumber$ placeholder is found.
        /// </summary>
        /// <param name="emailList">The list of <see cref="Email"/>.</param>
        /// <returns>The updated list of <see cref="Email"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailList"/> is null.</exception>
        private List<Email> InjectOrganizationNumbers(List<Email> emailList)
        {
            ArgumentNullException.ThrowIfNull(emailList);

            if (emailList.Count == 0)
            {
                return emailList;
            }

            var emailWithNationalIdentityNumber = emailList
                .Where(e => ContainsRecipientNumberPlaceholder(e.Subject) || ContainsRecipientNumberPlaceholder(e.Body))
                .Where(e => !string.IsNullOrEmpty(e.OrganizationNumber))
                .Distinct()
                .ToList();

            foreach (var emailWithKeyword in emailWithNationalIdentityNumber)
            {
                var email = emailList.Find(e => e.OrganizationNumber == emailWithKeyword.OrganizationNumber);
                if (email == null)
                {
                    continue;
                }

                email.Body = email.Body.Replace(_recipientNumberPlaceholder, email.OrganizationNumber ?? string.Empty);
                email.Subject = email.Subject.Replace(_recipientNumberPlaceholder, email.OrganizationNumber ?? string.Empty);
            }

            return emailList;
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
        /// Injects the recipient's organization name into the email where the $recipientName$ placeholder is found.
        /// </summary>
        /// <param name="emailList">The list of <see cref="Email"/>.</param>
        /// <returns>The updated list of <see cref="Email"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="emailList"/> is null.</exception>
        private async Task<List<Email>> InjectOrganizationNameAsync(List<Email> emailList)
        {
            ArgumentNullException.ThrowIfNull(emailList);

            if (emailList.Count == 0)
            {
                return emailList;
            }

            var organizationNumbers = emailList
                .Where(e => ContainsRecipientNamePlaceholder(e.Subject) || ContainsRecipientNamePlaceholder(e.Body))
                .Where(e => !string.IsNullOrEmpty(e.OrganizationNumber))
                .Select(e => e.OrganizationNumber)
                .Distinct()
                .ToList();

            if (organizationNumbers.Count == 0)
            {
                return emailList;
            }

            var partyDetails = await _registerClient.GetPartyDetailsForOrganizations(organizationNumbers);
            if (partyDetails == null || partyDetails.Count == 0)
            {
                return emailList;
            }

            foreach (var partyDetail in partyDetails)
            {
                var email = emailList.Find(e => e.OrganizationNumber == partyDetail.OrganizationNumber);
                if (email == null)
                {
                    continue;
                }

                email.Body = email.Body.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
                email.Subject = email.Subject.Replace(_recipientNamePlaceholder, partyDetail.Name ?? string.Empty);
            }

            return emailList;
        }
    }
}
