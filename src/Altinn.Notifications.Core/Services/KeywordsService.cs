using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

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
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="organizationNumbers"/> or <paramref name="nationalIdentityNumbers"/> is null.</exception>
    private async Task<(List<PartyDetails> PersonDetails, List<PartyDetails> OrganizationDetails)> FetchPartyDetailsAsync(
        List<string> organizationNumbers,
        List<string> nationalIdentityNumbers)
    {
        var partyDetails = await _registerClient.GetPartyDetails(nationalIdentityNumbers, organizationNumbers);

        var organizationDetails = partyDetails
            .Where(e => !string.IsNullOrWhiteSpace(e.OrganizationNumber) && organizationNumbers.Contains(e.OrganizationNumber))
            .ToList();

        var personDetails = partyDetails
            .Where(e => !string.IsNullOrWhiteSpace(e.NationalIdentityNumber) && nationalIdentityNumbers.Contains(e.NationalIdentityNumber))
            .ToList();

        return (personDetails, organizationDetails);
    }

    /// <summary>
    /// Replaces placeholders in the given text with actual values from the provided party details.
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

        customizedText = ReplaceWithDetails(customizedText, nationalIdentityNumber, personDetails, p => p.NationalIdentityNumber, true);

        return customizedText;
    }

    /// <summary>
    /// Replaces placeholders in the provided text with values from the matching party details.
    /// </summary>
    /// <param name="customizedText">The text containing placeholders to be replaced.</param>
    /// <param name="searchKey">The key used to locate a matching party details from the list of party details.</param>
    /// <param name="partyDetails">The list of party details to search for a match.</param>
    /// <param name="keySelector">A function to extract the key from a party detail for matching purposes.</param>
    /// <param name="isPerson">
    /// A flag indicating whether the detail represents a person. If <c>true</c>, the $recipientNumber$ placeholder will 
    /// be removed from the text. Otherwise, it will be replaced with the corresponding detail key value.
    /// </param>
    /// <returns>
    /// The text with placeholders replaced by values from the matching detail. If no match is found, the original text is returned.
    /// </returns>
    private static string? ReplaceWithDetails(
        string? customizedText,
        string? searchKey,
        IEnumerable<PartyDetails> partyDetails,
        Func<PartyDetails, string?> keySelector,
        bool isPerson = false)
    {
        if (string.IsNullOrWhiteSpace(customizedText) || string.IsNullOrWhiteSpace(searchKey))
        {
            return customizedText;
        }

        var matchingDetail = partyDetails.FirstOrDefault(detail => keySelector(detail) == searchKey);

        if (matchingDetail == null)
        {
            return customizedText;
        }

        // Replace the $recipientName$ placeholder with the detail's name or an empty string if null.
        customizedText = customizedText.Replace(_recipientNamePlaceholder, matchingDetail.Name ?? string.Empty, StringComparison.Ordinal);

        // Replace the $recipientNumber$ placeholder based on whether the detail represents a person or not.
        string recipientNumberReplacement = isPerson ? string.Empty : (keySelector(matchingDetail) ?? string.Empty);
        customizedText = customizedText.Replace(_recipientNumberPlaceholder, recipientNumberReplacement, StringComparison.Ordinal);

        return customizedText;
    }
}
