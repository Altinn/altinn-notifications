using System.Collections.Generic;
using System.Linq;
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
                .ReturnsAsync([new UserContactPoints() { NationalIdentityNumber = "12345678901", MobileNumber = "99999999", IsReserved = true }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddSmsContactPoints(input, null);

            // Assert 
            Assert.Equivalent(expectedOutput, input);
            string actualMobileNumber = ((SmsAddressPoint)input[0].AddressInfo[0]).MobileNumber;
            Assert.Equal("+4799999999", actualMobileNumber);
        }

        [Fact]
        public async Task AddSmsContactPoints_OrganizationNumberAvailable_RegisterServiceCalled()
        {
            // Arrange
            List<Recipient> input = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901",
                    AddressInfo = [new SmsAddressPoint("+4799999999")]
                }
            ];

            var registerClientMock = new Mock<IRegisterClient>();
            registerClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.Is<List<string>>(s => s.Contains("12345678901"))))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", MobileNumberList = ["+4799999999"] }]);

            var service = GetTestService(registerClient: registerClientMock.Object);

            // Act
            await service.AddSmsContactPoints(input, null);

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
            await service.AddEmailContactPoints(input, null);

            // Assert 
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddEmailContactPoints_OrganizationNumberAvailable_RegisterServiceCalled()
        {
            // Arrange
            List<Recipient> input = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901",
                    AddressInfo = [new EmailAddressPoint("email@domain.com")]
                }
            ];

            var registerClientMock = new Mock<IRegisterClient>();
            registerClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.Is<List<string>>(s => s.Contains("12345678901"))))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", EmailList = ["email@domain.com"] }]);

            var service = GetTestService(registerClient: registerClientMock.Object);

            // Act
            await service.AddEmailContactPoints(input, null);

            // Assert 
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddEmailContactPoints_OrganizationNumberAndResourceAvailable_AuthorizationPermitAll()
        {
            // Arrange
            string resource = "urn:altinn:resource";

            List<Recipient> input = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901",
                    AddressInfo = [new EmailAddressPoint("official@domain.com"), new EmailAddressPoint("user-1@domain.com"), new EmailAddressPoint("user-9@domain.com")]
                }
            ];

            var registerClientMock = new Mock<IRegisterClient>();
            registerClientMock
                .Setup(r => r.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", EmailList = ["official@domain.com"] }]);

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserRegisteredOrganizationContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = "12345678901",
                        UserContactPoints = [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                Email = "user-1@domain.com"
                            },
                            new UserContactPoints()
                            {
                                UserId = 200009,
                                Email = "user-9@domain.com"
                            }
                            ]
                    }
                    ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync((List<OrganizationContactPoints> input, string resource) => input);

            var service = GetTestService(profileClientMock.Object, registerClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(input, resource);

            // Assert 
            registerClientMock.VerifyAll();
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddEmailContactPoints_OrganizationNumberAndResourceAvailable_NoOfficialContact_AuthorizationDenyOne()
        {
            // Arrange
            string resource = "urn:altinn:resource";

            List<Recipient> input = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901",
                    AddressInfo = [new EmailAddressPoint("user-1@domain.com")]
                }
            ];

            var registerClientMock = new Mock<IRegisterClient>();
            registerClientMock
                .Setup(r => r.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([]);

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserRegisteredOrganizationContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = "12345678901",
                        UserContactPoints = [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                Email = "user-1@domain.com"
                            },
                            new UserContactPoints()
                            {
                                UserId = 200009,
                                Email = "user-9@domain.com"
                            }
                            ]
                    }
                    ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync((List<OrganizationContactPoints> input, string resource) =>
                {
                    input[0].UserContactPoints.RemoveAll(u => u.UserId == 200009);
                    return input;
                });

            var service = GetTestService(profileClientMock.Object, registerClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(input, resource);

            // Assert 
            registerClientMock.VerifyAll();
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddEmailContactPoints_OrganizationNumberAndResourceAvailable_AuthorizationDenyAll()
        {
            // Arrange
            string resource = "urn:altinn:resource";

            List<Recipient> input = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901"
                }
            ];

            List<Recipient> expectedOutput = [
                new Recipient()
                {
                    OrganizationNumber = "12345678901",
                    AddressInfo = [new EmailAddressPoint("official@domain.com")]
                }
            ];

            var registerClientMock = new Mock<IRegisterClient>();
            registerClientMock
                .Setup(r => r.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", EmailList = ["official@domain.com"] }]);

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserRegisteredOrganizationContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = "12345678901",
                        UserContactPoints = [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                Email = "user-1@domain.com"
                            },
                            new UserContactPoints()
                            {
                                UserId = 200009,
                                Email = "user-9@domain.com"
                            }
                            ]
                    }
                    ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync((List<OrganizationContactPoints> input, string resource) =>
                {
                    input.ForEach(ocp => ocp.UserContactPoints = []);
                    return input;
                });

            var service = GetTestService(profileClientMock.Object, registerClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(input, resource);

            // Assert 
            registerClientMock.VerifyAll();
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(expectedOutput, input);
        }

        private static ContactPointService GetTestService(
            IProfileClient? profileClient = null,
            IRegisterClient? registerClient = null,
            IAuthorizationService? authorizationService = null)
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

            if (authorizationService == null)
            {
                var authorizationServiceMock = new Mock<IAuthorizationService>();
                authorizationService = authorizationServiceMock.Object;
            }

            return new ContactPointService(profileClient, registerClient, authorizationService);
        }
    }
}
