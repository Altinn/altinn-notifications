using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class ContactPointServiceTests
    {
        [Fact]
        public async Task AddSmsContactPoints_NationalIdentityNumberAvailable_ProfileServiceCalled()
        {
            // Arrange
            List<Recipient> input = [
                new Recipient()
                {
                    NationalIdentityNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    NationalIdentityNumber = "12345678901",
                    IsReserved = true,
                    AddressInfo = [new SmsAddressPoint("+4799999999")]
                }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserContactPoints(It.Is<List<string>>(s => s.Contains("12345678901"))))
                .ReturnsAsync([new UserContactPoints() { NationalIdentityNumber = "12345678901", MobileNumber = "+4799999999", IsReserved = true }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddSmsContactPoints(input);

            // Assert 
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddEmailContactPoints_NationalIdentityNumberAvailable_ProfileServiceCalled()
        {
            // Arrange
            List<Recipient> input = [
                new Recipient()
                {
                    NationalIdentityNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    NationalIdentityNumber = "12345678901",
                    IsReserved = true,
                    AddressInfo = [new EmailAddressPoint("email@domain.com")]
                }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserContactPoints(It.Is<List<string>>(s => s.Contains("12345678901"))))
                .ReturnsAsync([new UserContactPoints() { NationalIdentityNumber = "12345678901", Email = "email@domain.com", IsReserved = true }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddEmailContactPoints(input);

            // Assert 
            Assert.Equivalent(expectedOutput, input);
        }

        private static ContactPointService GetTestService(IProfileClient? profileClient = null, IRegisterClient? registerClient = null)
        {
            if (profileClient == null)
            {
                var profileClientMock = new Mock<IProfileClient>();
                profileClient = profileClientMock.Object;
            }

            if (registerClient == null)
            {
                var registerClientMock = new Mock<IRegisterClient>();
                registerClient = registerClientMock.Object;
            }

            return new ContactPointService(profileClient, registerClient);
        }
    }
}
