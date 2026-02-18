using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IContactPointService"/> using Altinn platform services to lookup contact points
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ContactPointService"/> class.
/// </remarks>
public class ContactPointService(
    IProfileClient profile,
    IAuthorizationService authorizationService) : IContactPointService
{
    private const string _profileClientName = "ProfileClient";
    private const string _authorizationServiceName = "AuthorizationService";

    private readonly IProfileClient _profileClient = profile;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    /// <inheritdoc/>
    public async Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            ApplyEmailForPerson,
            ApplyEmailForOrganization,
            ApplyEmailForSelfIdentified);
    }

    /// <inheritdoc/>
    public async Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            ApplySmsForPerson,
            ApplySmsForOrganization,
            ApplySmsForSelfIdentified);
    }

    /// <inheritdoc/>
    public async Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            ApplyEmailAndSmsForPerson,
            ApplyEmailAndSmsForOrganization,
            ApplyEmailAndSmsForSelfIdentified);
    }

    /// <inheritdoc/>
    public async Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId)
    {
        switch (channel)
        {
            case NotificationChannel.EmailPreferred:
                await AugmentRecipients(
                    recipients,
                    resourceId,
                    ApplyEmailPreferredForPerson,
                    ApplyEmailPreferredForOrganization,
                    ApplyEmailPreferredForSelfIdentified);
                break;
            case NotificationChannel.SmsPreferred:
                await AugmentRecipients(
                    recipients,
                    resourceId,
                    ApplySmsPreferredForPerson,
                    ApplySmsPreferredForOrganization,
                    ApplySmsPreferredForSelfIdentified);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(channel), channel, $"Unsupported notification channel: {channel}");
        }
    }

    #region Person Contact Point Applicators

    private static void ApplyEmailForPerson(Recipient recipient, UserContactPoints userContactPoints)
    {
        if (!string.IsNullOrWhiteSpace(userContactPoints.Email))
        {
            recipient.AddressInfo.Add(new EmailAddressPoint(userContactPoints.Email));
        }
    }

    private static void ApplySmsForPerson(Recipient recipient, UserContactPoints userContactPoints)
    {
        if (!string.IsNullOrWhiteSpace(userContactPoints.MobileNumber))
        {
            recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
        }
    }

    private static void ApplyEmailAndSmsForPerson(Recipient recipient, UserContactPoints userContactPoints)
    {
        ApplyEmailForPerson(recipient, userContactPoints);
        ApplySmsForPerson(recipient, userContactPoints);
    }

    private static void ApplyEmailPreferredForPerson(Recipient recipient, UserContactPoints userContactPoints)
    {
        AddPreferredOrFallbackContactPoint(
            recipient,
            userContactPoints.Email,
            userContactPoints.MobileNumber,
            email => new EmailAddressPoint(email),
            mobile => new SmsAddressPoint(mobile));
    }

    private static void ApplySmsPreferredForPerson(Recipient recipient, UserContactPoints userContactPoints)
    {
        AddPreferredOrFallbackContactPoint(
           recipient,
           userContactPoints.MobileNumber,
           userContactPoints.Email,
           mobile => new SmsAddressPoint(mobile),
           email => new EmailAddressPoint(email));
    }

    #endregion

    #region Organization Contact Point Applicators

    private static void ApplyEmailForOrganization(Recipient recipient, OrganizationContactPoints orgContactPoints)
    {
        recipient.AddressInfo.AddRange(
            orgContactPoints.EmailList
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => new EmailAddressPoint(e)));

        recipient.AddressInfo.AddRange(
            orgContactPoints.UserContactPoints
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => new EmailAddressPoint(u.Email)));
    }

    private static void ApplySmsForOrganization(Recipient recipient, OrganizationContactPoints orgContactPoints)
    {
        recipient.AddressInfo.AddRange(
            orgContactPoints.MobileNumberList
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => new SmsAddressPoint(e)));

        recipient.AddressInfo.AddRange(
            orgContactPoints.UserContactPoints
                .Where(e => !string.IsNullOrWhiteSpace(e.MobileNumber))
                .Select(e => new SmsAddressPoint(e.MobileNumber)));
    }

    private static void ApplyEmailAndSmsForOrganization(Recipient recipient, OrganizationContactPoints orgContactPoints)
    {
        ApplyEmailForOrganization(recipient, orgContactPoints);
        ApplySmsForOrganization(recipient, orgContactPoints);
    }

    private static void ApplyEmailPreferredForOrganization(Recipient recipient, OrganizationContactPoints orgContactPoints)
    {
        AddPreferredOrFallbackContactPointList(
           recipient,
           orgContactPoints.EmailList,
           orgContactPoints.MobileNumberList,
           e => new EmailAddressPoint(e),
           m => new SmsAddressPoint(m));

        foreach (var userContact in orgContactPoints.UserContactPoints)
        {
            AddPreferredOrFallbackContactPoint(
                recipient,
                userContact.Email,
                userContact.MobileNumber,
                email => new EmailAddressPoint(email),
                mobile => new SmsAddressPoint(mobile));
        }
    }

    private static void ApplySmsPreferredForOrganization(Recipient recipient, OrganizationContactPoints orgContactPoints)
    {
        AddPreferredOrFallbackContactPointList(
           recipient,
           orgContactPoints.MobileNumberList,
           orgContactPoints.EmailList,
           e => new SmsAddressPoint(e),
           e => new EmailAddressPoint(e));

        foreach (var userContact in orgContactPoints.UserContactPoints)
        {
            AddPreferredOrFallbackContactPoint(
                recipient,
                userContact.MobileNumber,
                userContact.Email,
                mobile => new SmsAddressPoint(mobile),
                email => new EmailAddressPoint(email));
        }
    }

    #endregion

    #region Self-Identified Contact Point Applicators

    private static void ApplyEmailForSelfIdentified(Recipient recipient, SelfIdentifiedUserContactPoints selfIdentifiedContactPoints)
    {
        if (!string.IsNullOrWhiteSpace(selfIdentifiedContactPoints.Email))
        {
            recipient.AddressInfo.Add(new EmailAddressPoint(selfIdentifiedContactPoints.Email));
        }
    }

    private static void ApplySmsForSelfIdentified(Recipient recipient, SelfIdentifiedUserContactPoints selfIdentifiedContactPoints)
    {
        if (!string.IsNullOrWhiteSpace(selfIdentifiedContactPoints.MobileNumber))
        {
            recipient.AddressInfo.Add(new SmsAddressPoint(selfIdentifiedContactPoints.MobileNumber));
        }
    }

    private static void ApplyEmailAndSmsForSelfIdentified(Recipient recipient, SelfIdentifiedUserContactPoints selfIdentifiedContactPoints)
    {
        ApplyEmailForSelfIdentified(recipient, selfIdentifiedContactPoints);
        ApplySmsForSelfIdentified(recipient, selfIdentifiedContactPoints);
    }

    private static void ApplyEmailPreferredForSelfIdentified(Recipient recipient, SelfIdentifiedUserContactPoints selfIdentifiedContactPoints)
    {
        AddPreferredOrFallbackContactPoint(
            recipient,
            selfIdentifiedContactPoints.Email,
            selfIdentifiedContactPoints.MobileNumber,
            email => new EmailAddressPoint(email),
            mobile => new SmsAddressPoint(mobile));
    }

    private static void ApplySmsPreferredForSelfIdentified(Recipient recipient, SelfIdentifiedUserContactPoints selfIdentifiedContactPoints)
    {
        AddPreferredOrFallbackContactPoint(
           recipient,
           selfIdentifiedContactPoints.MobileNumber,
           selfIdentifiedContactPoints.Email,
           mobile => new SmsAddressPoint(mobile),
           email => new EmailAddressPoint(email));
    }

    #endregion

    private static void AddPreferredOrFallbackContactPoint<TPreferred, TFallback>(
        Recipient recipient,
        TPreferred preferredContact,
        TFallback fallbackContact,
        Func<TPreferred, IAddressPoint> preferredSelector,
        Func<TFallback, IAddressPoint> fallbackSelector)
    {
        if (!string.IsNullOrWhiteSpace(Convert.ToString(preferredContact)))
        {
            recipient.AddressInfo.Add(preferredSelector(preferredContact));
        }
        else if (!string.IsNullOrWhiteSpace(Convert.ToString(fallbackContact)))
        {
            recipient.AddressInfo.Add(fallbackSelector(fallbackContact));
        }
    }

    private static void AddPreferredOrFallbackContactPointList<TPreferred, TFallback>(
        Recipient recipient,
        List<TPreferred> preferredList,
        List<TFallback> fallbackList,
        Func<TPreferred, IAddressPoint> preferredSelector,
        Func<TFallback, IAddressPoint> fallbackSelector)
    {
        if (preferredList.Count > 0)
        {
            recipient.AddressInfo.AddRange(preferredList.Select(preferredSelector));
        }
        else
        {
            recipient.AddressInfo.AddRange(fallbackList.Select(fallbackSelector));
        }
    }

    /// <summary>
    /// Normalizes a resource identifier value by removing the leading
    /// 'urn:altinn:resource:' prefix if it is present.
    /// </summary>
    private static string GetSanitizedResourceId(string? resourceId)
    {
        var trimmedResourceId = resourceId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedResourceId))
        {
            return string.Empty;
        }

        const string prefix = "urn:altinn:resource:";

        return trimmedResourceId.StartsWith(prefix, StringComparison.Ordinal)
            ? trimmedResourceId[prefix.Length..]
            : trimmedResourceId;
    }

    /// <summary>
    /// Looks up and augments each recipient in the provided list with contact point information
    /// based on their national identity number, organization number, or external identity.
    /// </summary>
    /// <param name="recipients">
    /// The list of <see cref="Recipient"/> objects to be augmented with contact point information.
    /// Each recipient must have either a national identity number, organization number, or external identity.
    /// </param>
    /// <param name="resourceId">
    /// The resource identifier used to filter and authorize user-registered contact points for organizations.
    /// If <c>null</c> or empty, only official organization contact points are used.
    /// </param>
    /// <param name="applyPersonContactPoints">
    /// An action that applies contact points to the recipient in place. Invoked for recipients with a national identity number
    /// and a matching <see cref="UserContactPoints"/> entry.
    /// </param>
    /// <param name="applyOrganizationContactPoints">
    /// An action that applies contact points to the recipient in place. Invoked for recipients with an organization number
    /// and a matching <see cref="OrganizationContactPoints"/> entry.
    /// </param>
    /// <param name="applySelfIdentifiedUserContactPoints">
    /// An action that applies contact points to the recipient in place. Invoked for recipients with an external identity
    /// and a matching <see cref="SelfIdentifiedUserContactPoints"/> entry.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The method augments the provided recipient objects in place.
    /// </returns>
    private async Task AugmentRecipients(
        List<Recipient> recipients,
        string? resourceId,
        Action<Recipient, UserContactPoints> applyPersonContactPoints,
        Action<Recipient, OrganizationContactPoints> applyOrganizationContactPoints,
        Action<Recipient, SelfIdentifiedUserContactPoints> applySelfIdentifiedUserContactPoints)
    {
        var personLookupTask = LookupPersonContactPoints(recipients);
        var selfIdentifiedLookupTask = LookupSelfIdentifiedUserContactPoints(recipients);
        var organizationLookupTask = LookupOrganizationContactPoints(recipients, resourceId);

        await Task.WhenAll(personLookupTask, selfIdentifiedLookupTask, organizationLookupTask);

        List<UserContactPoints> personContactPointsList = personLookupTask.Result;
        List<OrganizationContactPoints> organizationContactPointsList = organizationLookupTask.Result;
        List<SelfIdentifiedUserContactPoints> selfIdentifiedContactPointsList = selfIdentifiedLookupTask.Result;

        foreach (Recipient recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber))
            {
                ApplyPersonContactPoints(recipient, personContactPointsList, applyPersonContactPoints);
            }
            else if (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber))
            {
                ApplyOrganizationContactPoints(recipient, organizationContactPointsList, applyOrganizationContactPoints);
            }
            else if (!string.IsNullOrWhiteSpace(recipient.ExternalIdentity))
            {
                ApplySelfIdentifiedContactPoints(recipient, selfIdentifiedContactPointsList, applySelfIdentifiedUserContactPoints);
            }
        }
    }

    private static void ApplyPersonContactPoints(
        Recipient recipient,
        List<UserContactPoints> personContactPointsList,
        Action<Recipient, UserContactPoints> applyContactPoints)
    {
        UserContactPoints? userContactPoints = personContactPointsList
            .Find(e => string.Equals(e.NationalIdentityNumber, recipient.NationalIdentityNumber, StringComparison.OrdinalIgnoreCase));

        if (userContactPoints == null)
        {
            return;
        }

        recipient.IsReserved = userContactPoints.IsReserved;
        applyContactPoints(recipient, userContactPoints);
    }

    private static void ApplyOrganizationContactPoints(
        Recipient recipient,
        List<OrganizationContactPoints> organizationContactPointsList,
        Action<Recipient, OrganizationContactPoints> applyContactPoints)
    {
        OrganizationContactPoints? organizationContactPoints = organizationContactPointsList
            .Find(e => string.Equals(e.OrganizationNumber, recipient.OrganizationNumber, StringComparison.OrdinalIgnoreCase));

        if (organizationContactPoints != null)
        {
            applyContactPoints(recipient, organizationContactPoints);
        }
    }

    private static void ApplySelfIdentifiedContactPoints(
        Recipient recipient,
        List<SelfIdentifiedUserContactPoints> selfIdentifiedContactPointsList,
        Action<Recipient, SelfIdentifiedUserContactPoints> applyContactPoints)
    {
        SelfIdentifiedUserContactPoints? selfIdentifiedContactPoints = selfIdentifiedContactPointsList
            .Find(e => string.Equals(e.ExternalIdentity, recipient.ExternalIdentity, StringComparison.OrdinalIgnoreCase));

        if (selfIdentifiedContactPoints != null)
        {
            applyContactPoints(recipient, selfIdentifiedContactPoints);
        }
    }

    /// <summary>
    /// Retrieves contact points for recipients with a valid national identity number.
    /// </summary>
    private async Task<List<UserContactPoints>> LookupPersonContactPoints(List<Recipient> recipients)
    {
        List<string> nationalIdentityNumbers = recipients
                .Where(e => !string.IsNullOrWhiteSpace(e.NationalIdentityNumber))
                .Select(e => e.NationalIdentityNumber!).ToList();

        if (nationalIdentityNumbers.Count == 0)
        {
            return [];
        }

        try
        {
            List<UserContactPoints> contactPoints = await _profileClient.GetUserContactPoints(nationalIdentityNumbers);

            contactPoints.ForEach(contactPoint =>
            {
                contactPoint.MobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(contactPoint.MobileNumber);

                if (!MobileNumberHelper.IsValidMobileNumber(contactPoint.MobileNumber))
                {
                    contactPoint.MobileNumber = string.Empty;
                }
            });

            return contactPoints;
        }
        catch (Exception ex) when (ex is not PlatformDependencyException)
        {
            throw new PlatformDependencyException(_profileClientName, "GetUserContactPoints", ex);
        }
    }

    /// <summary>
    /// Retrieves contact points for self-identified-users.
    /// </summary>
    private async Task<List<SelfIdentifiedUserContactPoints>> LookupSelfIdentifiedUserContactPoints(List<Recipient> recipients)
    {
        List<string> externalIdentities = [.. recipients
                .Where(e => !string.IsNullOrWhiteSpace(e.ExternalIdentity))
                .Select(e => e.ExternalIdentity!)];

        if (externalIdentities.Count == 0)
        {
            return [];
        }

        try
        {
            List<SelfIdentifiedUserContactPoints> contactPoints = await _profileClient.GetSelfIdentifiedUserContactPoints(externalIdentities);

            return [.. contactPoints.Select(contactPoint =>
            {
                var normalizedMobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(contactPoint.MobileNumber);
                var validMobileNumber = MobileNumberHelper.IsValidMobileNumber(normalizedMobileNumber) ? normalizedMobileNumber : string.Empty;

                return contactPoint with { MobileNumber = validMobileNumber };
            })];
        }
        catch (Exception ex) when (ex is not PlatformDependencyException)
        {
            throw new PlatformDependencyException(_profileClientName, "GetSelfIdentifiedUserContactPoints", ex);
        }
    }

    /// <summary>
    /// Retrieves contact points for recipients with a valid organization number.
    /// Optionally enriches the contact points with user-registered contact points authorized for a specific resource.
    /// </summary>
    private async Task<List<OrganizationContactPoints>> LookupOrganizationContactPoints(List<Recipient> recipients, string? resourceId)
    {
        List<string> organizationNumbers = recipients
            .Where(e => !string.IsNullOrWhiteSpace(e.OrganizationNumber))
            .Select(e => e.OrganizationNumber!).ToList();

        if (organizationNumbers.Count == 0)
        {
            return [];
        }

        List<OrganizationContactPoints> contactPoints = await GetOfficialOrganizationContactPoints(organizationNumbers);

        var sanitizedResourceId = GetSanitizedResourceId(resourceId);
        if (!string.IsNullOrWhiteSpace(sanitizedResourceId))
        {
            await EnrichWithUserRegisteredContactPoints(contactPoints, organizationNumbers, sanitizedResourceId);
        }

        CleanupOrganizationContactPoints(contactPoints);

        return contactPoints;
    }

    /// <summary>
    /// Retrieves official organization contact points from the profile service.
    /// </summary>
    private async Task<List<OrganizationContactPoints>> GetOfficialOrganizationContactPoints(List<string> organizationNumbers)
    {
        try
        {
            List<OrganizationContactPoints> contactPoints = await _profileClient.GetOrganizationContactPoints(organizationNumbers);

            contactPoints.ForEach(contactPoint =>
            {
                contactPoint.MobileNumberList = [.. contactPoint.MobileNumberList.Select(MobileNumberHelper.EnsureCountryCodeIfValidNumber)];
            });

            return contactPoints;
        }
        catch (Exception ex) when (ex is not PlatformDependencyException)
        {
            throw new PlatformDependencyException(_profileClientName, "GetOrganizationContactPoints", ex);
        }
    }

    /// <summary>
    /// Enriches organization contact points with authorized user-registered contact points.
    /// </summary>
    private async Task EnrichWithUserRegisteredContactPoints(
        List<OrganizationContactPoints> contactPoints,
        List<string> organizationNumbers,
        string sanitizedResourceId)
    {
        List<OrganizationContactPoints> allUserContactPoints = await GetUserRegisteredContactPoints(organizationNumbers, sanitizedResourceId);

        if (allUserContactPoints.Count == 0)
        {
            return;
        }

        List<OrganizationContactPoints> authorizedUserContactPoints = await AuthorizeUserContactPoints(allUserContactPoints, sanitizedResourceId);

        MergeUserContactPointsIntoOfficial(contactPoints, authorizedUserContactPoints);
    }

    /// <summary>
    /// Retrieves user-registered contact points from the profile service.
    /// </summary>
    private async Task<List<OrganizationContactPoints>> GetUserRegisteredContactPoints(
        List<string> organizationNumbers,
        string sanitizedResourceId)
    {
        try
        {
            return await _profileClient.GetUserRegisteredContactPoints(organizationNumbers, sanitizedResourceId);
        }
        catch (Exception ex) when (ex is not PlatformDependencyException)
        {
            throw new PlatformDependencyException(_profileClientName, "GetUserRegisteredContactPoints", ex);
        }
    }

    /// <summary>
    /// Authorizes user contact points for a specific resource.
    /// </summary>
    private async Task<List<OrganizationContactPoints>> AuthorizeUserContactPoints(
        List<OrganizationContactPoints> userContactPoints,
        string sanitizedResourceId)
    {
        try
        {
            return await _authorizationService.AuthorizeUserContactPointsForResource(userContactPoints, sanitizedResourceId);
        }
        catch (Exception ex) when (ex is not PlatformDependencyException)
        {
            throw new PlatformDependencyException(_authorizationServiceName, "AuthorizeUserContactPointsForResource", ex);
        }
    }

    /// <summary>
    /// Merges authorized user contact points into the official organization contact points list.
    /// </summary>
    private static void MergeUserContactPointsIntoOfficial(
        List<OrganizationContactPoints> officialContactPoints,
        List<OrganizationContactPoints> authorizedUserContactPoints)
    {
        foreach (var authorizedUserContactPoint in authorizedUserContactPoints)
        {
            var existingContactPoint = officialContactPoints
                .Find(e => string.Equals(e.OrganizationNumber, authorizedUserContactPoint.OrganizationNumber, StringComparison.OrdinalIgnoreCase));

            if (existingContactPoint == null)
            {
                officialContactPoints.Add(authorizedUserContactPoint);
            }
            else
            {
                existingContactPoint.UserContactPoints.AddRange(authorizedUserContactPoint.UserContactPoints);
            }
        }
    }

    /// <summary>
    /// Cleans up organization contact points by removing duplicates and invalid entries.
    /// </summary>
    private static void CleanupOrganizationContactPoints(List<OrganizationContactPoints> contactPoints)
    {
        contactPoints.ForEach(contactPoint =>
        {
            // Keep only unique and valid mobile numbers.
            contactPoint.MobileNumberList = [..
                contactPoint.MobileNumberList
                .Where(e => MobileNumberHelper.IsValidMobileNumber(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            // Keep only unique email addresses.
            contactPoint.EmailList = [..
                contactPoint.EmailList
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            // Keep only user contact points with unique and valid contact details.
            contactPoint.UserContactPoints = [..
                NullifyDuplicateContactAddress(contactPoint.UserContactPoints)
                .Select(userContact => NullifyDuplicateContactAddress(userContact, contactPoint))
                .Where(userContact => !string.IsNullOrWhiteSpace(userContact.Email) || MobileNumberHelper.IsValidMobileNumber(userContact.MobileNumber))
                ];
        });
    }

    /// <summary>
    /// Scans a collection of <see cref="UserContactPoints"/> and clears duplicate contact fields within the list.
    /// Only the first occurrence of each unique email address or mobile number is retained across the collection.
    /// Subsequent duplicates have the corresponding field set to an empty string.
    /// </summary>
    private static IEnumerable<UserContactPoints> NullifyDuplicateContactAddress(IEnumerable<UserContactPoints> userContacts)
    {
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMobiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var userContact in userContacts)
        {
            if (!string.IsNullOrWhiteSpace(userContact.Email))
            {
                var duplicateEmail = !seenEmails.Add(userContact.Email);
                if (duplicateEmail)
                {
                    userContact.Email = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(userContact.MobileNumber))
            {
                userContact.MobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(userContact.MobileNumber);

                var duplicateMobileNumber = !seenMobiles.Add(userContact.MobileNumber);
                if (duplicateMobileNumber)
                {
                    userContact.MobileNumber = string.Empty;
                }
            }

            yield return userContact;
        }
    }

    /// <summary>
    /// Scans the specified <see cref="UserContactPoints"/> for contact details (email and mobile number) 
    /// that are already present in the specified <see cref="OrganizationContactPoints"/>. 
    /// If a match is found, the corresponding field in the user contact is cleared (set to an empty string).
    /// </summary>
    private static UserContactPoints NullifyDuplicateContactAddress(UserContactPoints userContact, OrganizationContactPoints organizationContactPoints)
    {
        var isDuplicateEmail =
            !string.IsNullOrWhiteSpace(userContact.Email) &&
            organizationContactPoints.EmailList.Any(e => string.Equals(e, userContact.Email, StringComparison.OrdinalIgnoreCase));

        if (isDuplicateEmail)
        {
            userContact.Email = string.Empty;
        }

        var isDuplicateMobileNumber =
            !string.IsNullOrWhiteSpace(userContact.MobileNumber) &&
            organizationContactPoints.MobileNumberList.Any(m => string.Equals(m, userContact.MobileNumber, StringComparison.OrdinalIgnoreCase));

        if (isDuplicateMobileNumber)
        {
            userContact.MobileNumber = string.Empty;
        }

        return userContact;
    }
}
