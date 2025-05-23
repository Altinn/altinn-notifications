﻿using Altinn.Notifications.Core.Enums;
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
public class ContactPointService : IContactPointService
{
    private readonly IProfileClient _profileClient;
    private readonly IRegisterClient _registerClient;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactPointService"/> class.
    /// </summary>
    public ContactPointService(IProfileClient profile, IRegisterClient register, IAuthorizationService authorizationService)
    {
        _profileClient = profile;
        _registerClient = register;
        _authorizationService = authorizationService;
    }

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

    private async Task<List<Recipient>> AugmentRecipients(
        List<Recipient> recipients,
        string? resourceId,
        Func<Recipient, UserContactPoints, Recipient> createUserContactPoint,
        Func<Recipient, OrganizationContactPoints, Recipient> createOrgContactPoint)
    {
        List<Recipient> augmentedRecipients = [];

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
                    augmentedRecipients.Add(createUserContactPoint(recipient, userContactPoints));
                }
            }
            else if (!string.IsNullOrEmpty(recipient.OrganizationNumber))
            {
                OrganizationContactPoints? organizationContactPoints = organizationContactPointList!
                    .Find(o => o.OrganizationNumber == recipient.OrganizationNumber);

                if (organizationContactPoints != null)
                {
                    augmentedRecipients.Add(createOrgContactPoint(recipient, organizationContactPoints));
                }
            }
        }

        return augmentedRecipients;
    }

    private async Task<List<UserContactPoints>> LookupPersonContactPoints(List<Recipient> recipients)
    {
        List<string> nins = [.. recipients
                .Where(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                .Select(r => r.NationalIdentityNumber!)];

        if (nins.Count == 0)
        {
            return [];
        }

        List<UserContactPoints> contactPoints = await _profileClient.GetUserContactPoints(nins);

        contactPoints.ForEach(contactPoint =>
        {
            contactPoint.MobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(contactPoint.MobileNumber);
        });

        return contactPoints;
    }

    private async Task<List<OrganizationContactPoints>> LookupOrganizationContactPoints(List<Recipient> recipients, string? resourceId)
    {
        List<string> orgNos = [.. recipients
         .Where(r => !string.IsNullOrEmpty(r.OrganizationNumber))
         .Select(r => r.OrganizationNumber!)];

        if (orgNos.Count == 0)
        {
            return [];
        }

        List<OrganizationContactPoints> authorizedUserContactPoints = [];
        Task<List<OrganizationContactPoints>> registerTask = _registerClient.GetOrganizationContactPoints(orgNos);

        if (!string.IsNullOrEmpty(resourceId))
        {
            var allUserContactPoints = await _profileClient.GetUserRegisteredContactPoints(orgNos, resourceId);
            authorizedUserContactPoints = await _authorizationService.AuthorizeUserContactPointsForResource(allUserContactPoints, resourceId);
        }

        List<OrganizationContactPoints> contactPoints = await registerTask;

        if (!string.IsNullOrEmpty(resourceId))
        {
            foreach (var userContactPoint in authorizedUserContactPoints)
            {
                userContactPoint.UserContactPoints.ForEach(userContactPoint =>
                {
                    userContactPoint.MobileNumber = MobileNumberHelper.EnsureCountryCodeIfValidNumber(userContactPoint.MobileNumber);
                });

                var existingContactPoint = contactPoints.Find(cp => cp.OrganizationNumber == userContactPoint.OrganizationNumber);

                if (existingContactPoint != null)
                {
                    existingContactPoint.UserContactPoints.AddRange(userContactPoint.UserContactPoints);
                }
                else
                {
                    contactPoints.Add(userContactPoint);
                }
            }
        }

        contactPoints.ForEach(contactPoint =>
        {
            contactPoint.MobileNumberList = [.. contactPoint.MobileNumberList
                .Select(mobileNumber =>
                {
                    return MobileNumberHelper.EnsureCountryCodeIfValidNumber(mobileNumber);
                })];
        });

        return contactPoints;
    }
}
