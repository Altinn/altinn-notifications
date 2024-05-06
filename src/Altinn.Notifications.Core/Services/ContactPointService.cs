using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of the <see cref="IContactPointService"/> using Altinn platform services to lookup contact points
    /// </summary>
    public class ContactPointService : IContactPointService
    {
        private readonly IProfileClient _profileClient;
        private readonly IRegisterClient _registerClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactPointService"/> class.
        /// </summary>
        public ContactPointService(IProfileClient profile, IRegisterClient register)
        {
            _profileClient = profile;
            _registerClient = register;
        }

        /// <inheritdoc/>
        public async Task AddEmailContactPoints(List<Recipient> recipients)
        {
            await AugmentRecipients(
                recipients,
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
                    recipient.AddressInfo.AddRange(orgContactPoints.EmailList.Select(e => new EmailAddressPoint(e)).ToList());
                    return recipient;
                });
        }

        /// <inheritdoc/>
        public async Task AddSmsContactPoints(List<Recipient> recipients)
        {
            await AugmentRecipients(
                recipients,
                (recipient, userContactPoints) =>
                {
                    if (!string.IsNullOrEmpty(userContactPoints.MobileNumber.ToString()))
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                    }

                    return recipient;
                },
                (recipient, orgContactPoints) =>
                {
                    recipient.AddressInfo.AddRange(orgContactPoints.MobileNumberList.Select(m => new SmsAddressPoint(m)).ToList());
                    return recipient;
                });
        }

        private async Task<List<Recipient>> AugmentRecipients(
            List<Recipient> recipients,
            Func<Recipient, UserContactPoints, Recipient> createUserContactPoint,
            Func<Recipient, OrganizationContactPoints, Recipient> createOrgContactPoint)
        {
            List<Recipient> augmentedRecipients = [];

            var userLookupTask = LookupPersonContactPoints(recipients);
            var orgLookupTask = LookupOrganizationContactPoints(recipients);
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
            List<string> nins = recipients
                    .Where(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                    .Select(r => r.NationalIdentityNumber!)
                    .ToList();

            if (nins.Count == 0)
            {
                return new List<UserContactPoints>();
            }

            List<UserContactPoints> contactPoints = await _profileClient.GetUserContactPoints(nins);

            contactPoints.ForEach(contactPoint =>
            {
                contactPoint.MobileNumber.EnsureCountryCodeIfApplicable();
            });

            return contactPoints;
        }

        private async Task<List<OrganizationContactPoints>> LookupOrganizationContactPoints(List<Recipient> recipients)
        {
            /* the output from this function should include an AUHTORIZED list of user registered contact points if notification has a service affiliation 
                will require the extension of the OrganizationContactPoints class */
            List<string> orgNos = recipients
             .Where(r => !string.IsNullOrEmpty(r.OrganizationNumber))
             .Select(r => r.OrganizationNumber!)
             .ToList();

            if (orgNos.Count == 0)
            {
                return [];
            }

            List<OrganizationContactPoints> contactPoints = await _registerClient.GetOrganizationContactPoints(orgNos);

            contactPoints.ForEach(contactPoint =>
            {
                contactPoint.MobileNumberList
                    .ForEach(mobileNumber =>
                    {
                        mobileNumber.EnsureCountryCodeIfApplicable();
                    });
            });

            return contactPoints;
        }
    }
}
