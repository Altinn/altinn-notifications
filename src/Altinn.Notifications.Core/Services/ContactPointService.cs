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

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactPointService"/> class.
        /// </summary>
        public ContactPointService(IProfileClient profile)
        {
            _profileClient = profile;
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
                });
        }

        /// <inheritdoc/>
        public async Task AddSmsContactPoints(List<Recipient> recipients)
        {
            await AugmentRecipients(
                recipients,
                (recipient, userContactPoints) =>
                {
                    if (!string.IsNullOrEmpty(userContactPoints.MobileNumber))
                    {
                        recipient.AddressInfo.Add(new SmsAddressPoint(userContactPoints.MobileNumber));
                    }

                    return recipient;
                });
        }

        /// <inheritdoc/>
        public async Task<List<UserContactPointAvailability>> GetContactPointAvailability(List<Recipient> recipients)
        {
            return await LookupContactPointAvailability(recipients);
        }

        private async Task<List<Recipient>> AugmentRecipients(List<Recipient> recipients, Func<Recipient, UserContactPoints, Recipient> createContactPoint)
        {
            List<Recipient> augmentedRecipients = [];

            List<UserContactPoints> userContactPointsList = await LookupContactPoints(recipients);
            foreach (Recipient recipient in recipients)
            {
                if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber))
                {
                    UserContactPoints? userContactPoints = userContactPointsList!
                        .Find(u => u.NationalIdentityNumber == recipient.NationalIdentityNumber);

                    if (userContactPoints != null)
                    {
                        recipient.IsReserved = userContactPoints.IsReserved;
                        augmentedRecipients.Add(createContactPoint(recipient, userContactPoints));
                    }
                }
            }

            return augmentedRecipients;
        }

        private async Task<List<UserContactPoints>> LookupContactPoints(List<Recipient> recipients)
        {
            List<UserContactPoints> userContactPoints = new();
            List<string> nins = recipients
                    .Where(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                    .Select(r => r.NationalIdentityNumber!)
                    .ToList();

            Task<List<UserContactPoints>> ninLookupTask = nins.Count > 0
             ? _profileClient.GetUserContactPoints(nins)
             : Task.FromResult(new List<UserContactPoints>());

            await Task.WhenAll(ninLookupTask);

            userContactPoints.AddRange(ninLookupTask.Result);

            return userContactPoints;
        }

        private async Task<List<UserContactPointAvailability>> LookupContactPointAvailability(List<Recipient> recipients)
        {
            List<UserContactPointAvailability> contactPointAvailabilityList = new();

            List<string> nins = recipients
                    .Where(r => !string.IsNullOrEmpty(r.NationalIdentityNumber))
                    .Select(r => r.NationalIdentityNumber!)
                    .ToList();

            Task<List<UserContactPointAvailability>> ninLookupTask = nins.Count > 0
             ? _profileClient.GetUserContactPointAvailabilities(nins)
             : Task.FromResult(new List<UserContactPointAvailability>());

            await Task.WhenAll(ninLookupTask);

            contactPointAvailabilityList.AddRange(ninLookupTask.Result);

            return contactPointAvailabilityList;
        }
    }
}
