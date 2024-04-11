using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels
{
    public class RecipientTests
    {
        [Fact]
        public void DeepCopy_ReturnsNewInstance_ObjectsAreEquivalent()
        {
            // Arrange
            var recipient = new Recipient()
            {
                NationalIdentityNumber = "16069412345",
                IsReserved = true,
                AddressInfo = [new SmsAddressPoint("+4781549300"), new EmailAddressPoint("test@digdir.no")]
            };

            // Act
            var result = recipient.DeepCopy();

            // Assert
            Assert.NotSame(recipient, result);
            Assert.Equivalent(recipient, result);
        }
    }
}
