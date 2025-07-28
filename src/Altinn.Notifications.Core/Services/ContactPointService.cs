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
                if (!string.IsNullOrEmpty(userContactPoints.Email))
                {
                    recipient.AddressInfo.Add(new EmailAddressPoint(userContactPoints.Email));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.EmailList.Select(e => new EmailAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrEmpty(u.Email)).Select(u => new EmailAddressPoint(u.Email)));

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
                if (!string.IsNullOrEmpty(userContactPoints.MobileNumber))
                {
                    recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.MobileNumberList.Select(m => new SmsAddressPoint(m)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrEmpty(u.MobileNumber)).Select(u => new SmsAddressPoint(u.MobileNumber)));

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
                if (!string.IsNullOrEmpty(userContactPoints.Email))
                {
                    recipient.AddressInfo.Add(new EmailAddressPoint(userContactPoints.Email));
                }

                if (!string.IsNullOrEmpty(userContactPoints.MobileNumber))
                {
                    recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                }

                return recipient;
            },
            (recipient, orgContactPoints) =>
            {
                recipient.AddressInfo.AddRange(orgContactPoints.EmailList.Select(e => new EmailAddressPoint(e)));

                recipient.AddressInfo.AddRange(orgContactPoints.MobileNumberList.Select(m => new SmsAddressPoint(m)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrEmpty(u.Email)).Select(u => new EmailAddressPoint(u.Email)));

                recipient.AddressInfo.AddRange(orgContactPoints.UserContactPoints.Where(u => !string.IsNullOrEmpty(u.MobileNumber)).Select(u => new SmsAddressPoint(u.MobileNumber)));

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
                       m => new SmsAddressPoint(m),
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
        if (!string.IsNullOrEmpty(preferredContact?.ToString()))
        {
            recipient.AddressInfo.Add(preferredSelector(preferredContact));
        }
        else if (!string.IsNullOrEmpty(fallbackContact?.ToString()))
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
        List<OrganizationContactPoints> organizationContactPointList = orgLookupTask.Result;

        foreach (Recipient recipient in recipients)
        {
            if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber))
            {
                UserContactPoints? userContactPoints = userContactPointsList!
                    .Find(u => u.NationalIdentityNumber == recipient.NationalIdentityNumber);

                if (userContactPoints != null)
                {
                    recipient.IsReserved = userContactPoints.IsReserved;
                    createUserContactPoint(recipient, userContactPoints);
                }
            }
            else if (!string.IsNullOrEmpty(recipient.OrganizationNumber))
            {
                OrganizationContactPoints? organizationContactPoints = organizationContactPointList!
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

        if (!string.IsNullOrEmpty(resourceId))
        {
            var allUserContactPoints = await _profileClient.GetUserRegisteredContactPoints(organizationNumbers, resourceId);
            var authorizedUserContactPoints = await _authorizationService.AuthorizeUserContactPointsForResource(allUserContactPoints, resourceId);

            foreach (var authorizedUserContactPoint in authorizedUserContactPoints)
            {
                authorizedUserContactPoint.UserContactPoints.ForEach(userContactPoint =>
                {
                    userContactPoint.MobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(userContactPoint.MobileNumber);
                });

                var existingContactPoint = contactPoints.Find(cp => cp.OrganizationNumber == authorizedUserContactPoint.OrganizationNumber);
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
            contactPoint.MobileNumberList = [.. contactPoint.MobileNumberList
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            contactPoint.EmailList = [.. contactPoint.EmailList
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            contactPoint.MobileNumberList = [.. contactPoint.MobileNumberList
                .Select(mobileNumber =>
                {
                    return MobileNumberHelper.EnsureCountryCodeIfValidNumber(mobileNumber);
                })];
        });

        return contactPoints;
    }
}
