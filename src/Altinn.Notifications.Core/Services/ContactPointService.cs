using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Services.Interfaces;

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
    private readonly IProfileClient _profileClient = profile;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    /// <inheritdoc/>
    public async Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            (recipient, userContactPoints) =>
            {
                if (!string.IsNullOrWhiteSpace(userContactPoints.Email))
                {
                    recipient.AddressInfo.Add(new EmailAddressPoint(userContactPoints.Email));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.EmailList.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new EmailAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrWhiteSpace(u.Email)).Select(u => new EmailAddressPoint(u.Email)));

                return recipient;
            });
    }

    /// <inheritdoc/>
    public async Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            (recipient, userContactPoints) =>
            {
                if (!string.IsNullOrWhiteSpace(userContactPoints.MobileNumber))
                {
                    recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.MobileNumberList.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new SmsAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(e => !string.IsNullOrWhiteSpace(e.MobileNumber)).Select(e => new SmsAddressPoint(e.MobileNumber)));

                return recipient;
            });
    }

    /// <inheritdoc/>
    public async Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            (recipient, userContactPoints) =>
            {
                if (!string.IsNullOrWhiteSpace(userContactPoints.Email))
                {
                    recipient.AddressInfo.Add(new EmailAddressPoint(userContactPoints.Email));
                }

                if (!string.IsNullOrWhiteSpace(userContactPoints.MobileNumber))
                {
                    recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.EmailList.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new EmailAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.MobileNumberList.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => new SmsAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrWhiteSpace(u.Email)).Select(u => new EmailAddressPoint(u.Email)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrWhiteSpace(u.MobileNumber)).Select(u => new SmsAddressPoint(u.MobileNumber)));

                return recipient;
            });
    }

    /// <inheritdoc/>
    public async Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId)
    {
        await AugmentRecipients(
            recipients,
            resourceId,
            (recipient, userContactPoints) =>
            {
                if (channel == NotificationChannel.EmailPreferred)
                {
                    AddPreferredOrFallbackContactPoint(
                        recipient,
                        userContactPoints.Email,
                        userContactPoints.MobileNumber,
                        email => new EmailAddressPoint(email),
                        mobile => new SmsAddressPoint(mobile));
                }
                else if (channel == NotificationChannel.SmsPreferred)
                {
                    AddPreferredOrFallbackContactPoint(
                       recipient,
                       userContactPoints.MobileNumber,
                       userContactPoints.Email,
                       mobile => new SmsAddressPoint(mobile),
                       email => new EmailAddressPoint(email));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                if (channel == NotificationChannel.EmailPreferred)
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
                else if (channel == NotificationChannel.SmsPreferred)
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

                return recipient;
            });
    }

    private static string GetSanitizedResourceId(string resourceId)
    {
        return resourceId.StartsWith("urn:altinn:resource:", StringComparison.Ordinal) ? resourceId["urn:altinn:resource:".Length..] : resourceId;
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

    /// <summary>
    /// Looks up and augments each recipient in the provided list with contact point information
    /// based on their national identity number or organization number.
    /// </summary>
    /// <param name="recipients">
    /// The list of <see cref="Recipient"/> objects to be augmented with contact point information.
    /// Each recipient must have either a national identity number or an organization number.
    /// </param>
    /// <param name="resourceId">
    /// The resource identifier used to filter and authorize user-registered contact points for organizations.
    /// If <c>null</c> or empty, only official organization contact points are used.
    /// </param>
    /// <param name="createUserContactPoint">
    /// A function that applies user contact point data to a recipient. Invoked for recipients with a national identity number
    /// and a matching <see cref="UserContactPoints"/> entry.
    /// </param>
    /// <param name="createOrgContactPoint">
    /// A function that applies organization contact point data to a recipient. Invoked for recipients with an organization number
    /// and a matching <see cref="OrganizationContactPoints"/> entry.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The method augments the provided recipient objects in place.
    /// </returns>
    private async Task AugmentRecipients(
        List<Recipient> recipients,
        string? resourceId,
        Func<Recipient, UserContactPoints, Recipient> createUserContactPoint,
        Func<Recipient, OrganizationContactPoints, Recipient> createOrgContactPoint)
    {
        var userLookupTask = LookupPersonContactPoints(recipients);
        var orgLookupTask = LookupOrganizationContactPoints(recipients, resourceId);

        await Task.WhenAll(userLookupTask, orgLookupTask);

        List<UserContactPoints> userContactPointsList = userLookupTask.Result;
        List<OrganizationContactPoints> organizationContactPointsList = orgLookupTask.Result;

        foreach (Recipient recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber))
            {
                UserContactPoints? userContactPoints = userContactPointsList
                    .Find(e => e.NationalIdentityNumber == recipient.NationalIdentityNumber);

                if (userContactPoints != null)
                {
                    recipient.IsReserved = userContactPoints.IsReserved;
                    createUserContactPoint(recipient, userContactPoints);
                }
            }
            else if (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber))
            {
                OrganizationContactPoints? organizationContactPoints = organizationContactPointsList
                    .Find(o => o.OrganizationNumber == recipient.OrganizationNumber);

                if (organizationContactPoints != null)
                {
                    createOrgContactPoint(recipient, organizationContactPoints);
                }
            }
        }
    }

    /// <summary>
    /// Retrieves contact points for recipients with a valid national identity number.
    /// </summary>
    /// <param name="recipients">
    /// The list of <see cref="Recipient"/> objects to retrieve contact information for. 
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a list of <see cref="UserContactPoints"/> 
    /// corresponding to the provided national identity numbers. If no valid national identity numbers are found, an empty list is returned.
    /// </returns>
    private async Task<List<UserContactPoints>> LookupPersonContactPoints(List<Recipient> recipients)
    {
        List<string> nationalIdentityNumbers = [.. recipients
                .Where(e => !string.IsNullOrWhiteSpace(e.NationalIdentityNumber))
                .Select(e => e.NationalIdentityNumber)];

        if (nationalIdentityNumbers.Count == 0)
        {
            return [];
        }

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

    /// <summary>
    /// Retrieves contact points for recipients with a valid organization number.
    /// Optionally enriches the contact points with user-registered contact points authorized for a specific resource.
    /// </summary>
    /// <param name="recipients">
    /// The list of <see cref="Recipient"/> objects to retrieve organization contact information for.
    /// </param>
    /// <param name="resourceId">
    /// The resource identifier used to filter and authorize user-registered contact points for the organizations.
    /// If <c>null</c> or empty, only official organization contact points are returned.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a list of <see cref="OrganizationContactPoints"/>
    /// corresponding to the provided organization numbers. If no valid organization numbers are found, an empty list is returned.
    /// </returns>
    private async Task<List<OrganizationContactPoints>> LookupOrganizationContactPoints(List<Recipient> recipients, string? resourceId)
    {
        List<string> organizationNumbers = [.. recipients
            .Where(e => !string.IsNullOrWhiteSpace(e.OrganizationNumber))
            .Select(e => e.OrganizationNumber)];

        if (organizationNumbers.Count == 0)
        {
            return [];
        }

        List<OrganizationContactPoints> contactPoints = await _profileClient.GetOrganizationContactPoints(organizationNumbers);
        contactPoints.ForEach(contactPoint =>
        {
            contactPoint.MobileNumberList = [.. contactPoint.MobileNumberList.Select(MobileNumberHelper.EnsureCountryCodeIfValidNumber)];
        });

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            var sanitizedResourceId = GetSanitizedResourceId(resourceId);

            var allUserContactPoints = await _profileClient.GetUserRegisteredContactPoints(organizationNumbers, sanitizedResourceId);

            var authorizedUserContactPoints = await _authorizationService.AuthorizeUserContactPointsForResource(allUserContactPoints, sanitizedResourceId);

            foreach (var authorizedUserContactPoint in authorizedUserContactPoints)
            {
                var existingContactPoint = contactPoints.Find(e => e.OrganizationNumber == authorizedUserContactPoint.OrganizationNumber);
                if (existingContactPoint == null)
                {
                    contactPoints.Add(authorizedUserContactPoint);
                }
                else
                {
                    existingContactPoint.UserContactPoints.AddRange(authorizedUserContactPoint.UserContactPoints);
                }
            }
        }

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

            // Keep only unique and valid mobile numbers.
            contactPoint.UserContactPoints = [..
                NullifyDuplicateContactAddress(contactPoint.UserContactPoints)
                .Select(userContact => NullifyDuplicateContactAddress(userContact, contactPoint))
                .Where(userContact => !string.IsNullOrWhiteSpace(userContact.Email) || MobileNumberHelper.IsValidMobileNumber(userContact.MobileNumber))
                ];
        });

        return contactPoints;
    }

    /// <summary>
    /// Scans a collection of <see cref="UserContactPoints"/> and clears duplicate contact fields within the list.
    /// Only the first occurrence of each unique email address or mobile number is retained across the collection.
    /// Subsequent duplicates have the corresponding field set to an empty string.
    /// </summary>
    /// <param name="userContacts">
    /// The collection of user contact points to process for internal deduplication.
    /// </param>
    /// <returns>
    /// A sequence of <see cref="UserContactPoints"/> where duplicate email and mobile number values have been nullified.
    /// </returns>
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
    /// <param name="userContact">
    /// The user contact information to evaluate and modify if duplicates are found.
    /// </param>
    /// <param name="organizationContactPoints">
    /// The organization's official contact point data containing email addresses and mobile numbers.
    /// </param>
    /// <returns>
    /// The same <see cref="UserContactPoints"/> instance with duplicate email and/or mobile number fields cleared.
    /// If both fields become empty, the caller is responsible for removing the contact from the list.
    /// </returns>
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
