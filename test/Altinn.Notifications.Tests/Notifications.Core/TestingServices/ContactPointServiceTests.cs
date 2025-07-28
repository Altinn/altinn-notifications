using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
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
        public async Task AddSmsContactPoints_WithValidNationalId_EnrichesRecipientWithFormattedSmsContactPoint()
        {
            // Arrange
            const string nationalId = "12345678901";
            const string rawMobileNumber = "99999999";
            const string formattedMobileNumber = "+4799999999";

            var recipients = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var mockUserContactPoints = new List<UserContactPoints>
            {
                new()
                {
                    IsReserved = true,
                    MobileNumber = rawMobileNumber,
                    NationalIdentityNumber = nationalId
                }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(x => x.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))))
                .ReturnsAsync(mockUserContactPoints);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddSmsContactPoints(recipients, null);

            // Assert
            var recipient = recipients[0];
            Assert.True(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Single(recipient.AddressInfo);
            var smsAddressPoint = Assert.IsType<SmsAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))), Times.Once);
        }

        [Fact]
        public async Task AddSmsContactPoints_WithValidOrganizationNumber_EnrichesRecipientWithOrganizationSmsContact()
        {
            // Arrange
            const string organizationNumber = "123456789";
            const string expectedMobileNumber = "+4799999999";

            var recipients = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var mockOrganizationContactPoints = new List<OrganizationContactPoints>
            {
                new()
                {
                    OrganizationNumber = organizationNumber,
                    MobileNumberList = [expectedMobileNumber]
                }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(x => x.GetOrganizationContactPoints(It.Is<List<string>>(orgs => orgs.Contains(organizationNumber))))
                .ReturnsAsync(mockOrganizationContactPoints);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddSmsContactPoints(recipients, null);

            // Assert
            var recipient = recipients[0];
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);
            Assert.Single(recipient.AddressInfo);

            var smsAddressPoint = Assert.IsType<SmsAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(expectedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(x => x.GetOrganizationContactPoints(It.Is<List<string>>(orgs => orgs.Contains(organizationNumber))), Times.Once);
        }

        [Fact]
        public async Task AddSmsContactPoints_OrganizationNumberAndResourceAvailable_AuthorizationPermitAll()
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
                    AddressInfo = [new SmsAddressPoint("+4799999999"), new SmsAddressPoint("+4748123456"), new SmsAddressPoint("+4699999999")]
                }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", MobileNumberList = ["+4799999999"] }]);

            profileClientMock
                .Setup(p => p.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = "12345678901",
                        UserContactPoints = [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                MobileNumber = "+4748123456",
                                Email = "user-1@domain.com"
                            },
                            new UserContactPoints()
                            {
                                UserId = 200009,
                                MobileNumber = "004699999999",
                                Email = "user-9@domain.com"
                            }
                        ]
                    }
                    ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync((List<OrganizationContactPoints> input, string resource) => input);

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddSmsContactPoints(input, resource);

            // Assert 
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
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

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.Is<List<string>>(s => s.Contains("12345678901"))))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", EmailList = ["email@domain.com"] }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddEmailContactPoints(input, null);

            // Assert 
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

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([]);

            profileClientMock
                .Setup(p => p.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = "12345678901",
                        UserContactPoints = [
                          new UserContactPoints()
                          {
                              UserId = 200001,
                              MobileNumber = "+4748123456",
                              Email = "user-1@domain.com"
                          },
                            new UserContactPoints()
                            {
                                UserId = 200009,
                                MobileNumber = "004699999999",
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

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(input, resource);

            // Assert 
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

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([new OrganizationContactPoints() { OrganizationNumber = "12345678901", EmailList = ["official@domain.com"] }]);

            profileClientMock
                .Setup(p => p.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.Is<string>(s => s.Equals("urn:altinn:resource"))))
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

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(input, resource);

            // Assert 
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(expectedOutput, input);
        }

        [Fact]
        public async Task AddPreferredContactPoints_EmailPreferred_PersonRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { NationalIdentityNumber = "0" },
                new Recipient() { NationalIdentityNumber = "1" },
                new Recipient() { NationalIdentityNumber = "2" },
                new Recipient() { NationalIdentityNumber = "3" }
            ];

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(p => p.GetUserContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([
                    new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                    new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                    new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                    new UserContactPoints() { NationalIdentityNumber = "3" }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipients, null);

            // Assert
            Recipient user0 = recipients[0];
            Assert.Single(user0.AddressInfo);
            Assert.IsType<EmailAddressPoint>(user0.AddressInfo[0]);
            Assert.Equal("user0@email.com", ((EmailAddressPoint)user0.AddressInfo[0]).EmailAddress);

            Recipient user1 = recipients[1];
            Assert.Single(user1.AddressInfo);
            Assert.IsType<SmsAddressPoint>(user1.AddressInfo[0]);
            Assert.Equal("+4799999991", ((SmsAddressPoint)user1.AddressInfo[0]).MobileNumber);

            Recipient user2 = recipients[2];
            Assert.Single(user2.AddressInfo);
            Assert.IsType<EmailAddressPoint>(user2.AddressInfo[0]);
            Assert.Equal("user2@email.com", ((EmailAddressPoint)user2.AddressInfo[0]).EmailAddress);

            Recipient user3 = recipients[3];
            Assert.Empty(user3.AddressInfo);
        }

        [Fact]
        public async Task AddPreferredContactPoints_SmsPreferred_PersonRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { NationalIdentityNumber = "0" },
                new Recipient() { NationalIdentityNumber = "1" },
                new Recipient() { NationalIdentityNumber = "2" },
                new Recipient() { NationalIdentityNumber = "3" },
            ];

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(p => p.GetUserContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([
                    new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                    new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                    new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                    new UserContactPoints() { NationalIdentityNumber = "3" }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipients, null);

            // Assert
            Recipient user0 = recipients[0];
            Assert.Single(user0.AddressInfo);
            Assert.IsType<EmailAddressPoint>(user0.AddressInfo[0]);
            Assert.Equal("user0@email.com", ((EmailAddressPoint)user0.AddressInfo[0]).EmailAddress);

            Recipient user1 = recipients[1];
            Assert.Single(user1.AddressInfo);
            Assert.IsType<SmsAddressPoint>(user1.AddressInfo[0]);
            Assert.Equal("+4799999991", ((SmsAddressPoint)user1.AddressInfo[0]).MobileNumber);

            Recipient user2 = recipients[2];
            Assert.Single(user2.AddressInfo);
            Assert.IsType<SmsAddressPoint>(user2.AddressInfo[0]);
            Assert.Equal("+4799999992", ((SmsAddressPoint)user2.AddressInfo[0]).MobileNumber);

            Recipient user3 = recipients[3];
            Assert.Empty(user3.AddressInfo);
        }

        [Fact]
        public async Task AddPreferredContactPoints_EmailPreferred_OrgRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { OrganizationNumber = "0" },
                new Recipient() { OrganizationNumber = "1" },
                new Recipient() { OrganizationNumber = "2" },
                new Recipient() { OrganizationNumber = "3" },
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([
                    new OrganizationContactPoints() { OrganizationNumber = "0", EmailList = ["0@org.com"] },
                    new OrganizationContactPoints() { OrganizationNumber = "1", MobileNumberList = ["+4799999991"] },
                    new OrganizationContactPoints() { OrganizationNumber = "2", EmailList = ["2@org.com"], MobileNumberList = ["+4799999992"] },
                    new OrganizationContactPoints() { OrganizationNumber = "3" }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipients, null);

            // Assert
            Recipient org0 = recipients[0];
            Assert.Single(org0.AddressInfo);
            Assert.IsType<EmailAddressPoint>(org0.AddressInfo[0]);
            Assert.Equal("0@org.com", ((EmailAddressPoint)org0.AddressInfo[0]).EmailAddress);

            Recipient org1 = recipients[1];
            Assert.Single(org1.AddressInfo);
            Assert.IsType<SmsAddressPoint>(org1.AddressInfo[0]);
            Assert.Equal("+4799999991", ((SmsAddressPoint)org1.AddressInfo[0]).MobileNumber);

            Recipient org2 = recipients[2];
            Assert.Single(org2.AddressInfo);
            Assert.IsType<EmailAddressPoint>(org2.AddressInfo[0]);
            Assert.Equal("2@org.com", ((EmailAddressPoint)org2.AddressInfo[0]).EmailAddress);

            Recipient org3 = recipients[3];
            Assert.Empty(org3.AddressInfo);
        }

        [Fact]
        public async Task AddPreferredContactPoints_SmsPreferred_OrgOfficialRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { OrganizationNumber = "0" },
                new Recipient() { OrganizationNumber = "1" },
                new Recipient() { OrganizationNumber = "2" },
                new Recipient() { OrganizationNumber = "3" },
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([
                    new OrganizationContactPoints() { OrganizationNumber = "0", EmailList = ["0@org.com"] },
                    new OrganizationContactPoints() { OrganizationNumber = "1", MobileNumberList = ["+4799999991"] },
                    new OrganizationContactPoints() { OrganizationNumber = "2", EmailList = ["2@org.com"], MobileNumberList = ["+4799999992"] },
                    new OrganizationContactPoints() { OrganizationNumber = "3" }]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipients, null);

            // Assert
            Recipient org0 = recipients[0];
            Assert.Single(org0.AddressInfo);
            Assert.IsType<EmailAddressPoint>(org0.AddressInfo[0]);
            Assert.Equal("0@org.com", ((EmailAddressPoint)org0.AddressInfo[0]).EmailAddress);

            Recipient org1 = recipients[1];
            Assert.Single(org1.AddressInfo);
            Assert.IsType<SmsAddressPoint>(org1.AddressInfo[0]);
            Assert.Equal("+4799999991", ((SmsAddressPoint)org1.AddressInfo[0]).MobileNumber);

            Recipient org2 = recipients[2];
            Assert.Single(org2.AddressInfo);
            Assert.IsType<SmsAddressPoint>(org2.AddressInfo[0]);
            Assert.Equal("+4799999992", ((SmsAddressPoint)org2.AddressInfo[0]).MobileNumber);

            Recipient org3 = recipients[3];
            Assert.Empty(org3.AddressInfo);
        }

        [Fact]
        public async Task AddPreferredContactPoints_EmailPreferred_OrgUserRegisteredRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { OrganizationNumber = "0" }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([]);

            profileClientMock
                .Setup(p => p.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        OrganizationNumber = "0",
                        UserContactPoints =
                        [
                            new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                            new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                            new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                            new UserContactPoints() { NationalIdentityNumber = "3" }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        OrganizationNumber = "0",
                        UserContactPoints =
                        [
                            new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                            new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                            new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                            new UserContactPoints() { NationalIdentityNumber = "3" }
                        ]
                    }
                ]);

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipients, "resource");

            // Assert
            Recipient org0 = recipients[0];
            Assert.Equal(3, org0.AddressInfo.Count);
            Assert.Equal("user0@email.com", ((EmailAddressPoint)org0.AddressInfo[0]).EmailAddress);
            Assert.Equal("+4799999991", ((SmsAddressPoint)org0.AddressInfo[1]).MobileNumber);
            Assert.Equal("user2@email.com", ((EmailAddressPoint)org0.AddressInfo[2]).EmailAddress);
        }

        [Fact]
        public async Task AddPreferredContactPoints_SmsPreferred_OrgUserRegisteredRecipient()
        {
            // Arrange
            List<Recipient> recipients = [
                new Recipient() { OrganizationNumber = "0" }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.IsAny<List<string>>()))
                .ReturnsAsync([]);

            profileClientMock
                .Setup(p => p.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        OrganizationNumber = "0",
                        UserContactPoints =
                        [
                            new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                            new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                            new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                            new UserContactPoints() { NationalIdentityNumber = "3" }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            authorizationServiceMock
                .Setup(a => a.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new OrganizationContactPoints()
                    {
                        OrganizationNumber = "0",
                        UserContactPoints =
                        [
                            new UserContactPoints() { NationalIdentityNumber = "0", Email = "user0@email.com" },
                            new UserContactPoints() { NationalIdentityNumber = "1", MobileNumber = "+4799999991" },
                            new UserContactPoints() { NationalIdentityNumber = "2", Email = "user2@email.com", MobileNumber = "+4799999992" },
                            new UserContactPoints() { NationalIdentityNumber = "3" }
                        ]
                    }
                ]);

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipients, "resource");

            // Assert
            Recipient org0 = recipients[0];
            Assert.Equal(3, org0.AddressInfo.Count);
            Assert.Equal("user0@email.com", ((EmailAddressPoint)org0.AddressInfo[0]).EmailAddress);
            Assert.Equal("+4799999991", ((SmsAddressPoint)org0.AddressInfo[1]).MobileNumber);
            Assert.Equal("+4799999992", ((SmsAddressPoint)org0.AddressInfo[2]).MobileNumber);
        }

        [Fact]
        public async Task AddEmailAndSmsContactPointsAsync_OrganizationNumber_RegisterServiceCalled()
        {
            // Arrange
            string mobileNumber = "+4799999999";
            string email = "recipient@domain.com";
            string organizationNumber = "18874198354";

            List<Recipient> recipients =
            [
                new Recipient { OrganizationNumber = organizationNumber }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints
                    {
                        EmailList = [email],
                        MobileNumberList = [mobileNumber],
                        OrganizationNumber = organizationNumber
                    }
                ]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddEmailAndSmsContactPointsAsync(recipients, null);

            // Assert
            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(s => s.Contains(organizationNumber))), Times.Once);

            Assert.Equal(2, recipients[0].AddressInfo.Count);
            Assert.Equal(organizationNumber, recipients[0].OrganizationNumber);

            var emailAddress = recipients[0].AddressInfo.OfType<EmailAddressPoint>().FirstOrDefault();
            Assert.NotNull(emailAddress);
            Assert.Equal(email, emailAddress.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddress.AddressType);

            var smsAddress = recipients[0].AddressInfo.OfType<SmsAddressPoint>().FirstOrDefault();
            Assert.NotNull(smsAddress);
            Assert.Equal(mobileNumber, smsAddress.MobileNumber);
            Assert.Equal(AddressType.Sms, smsAddress.AddressType);
        }

        [Fact]
        public async Task AddEmailAndSmsContactPointsAsync_NationalIdentityNumber_ProfileServiceCalled()
        {
            // Arrange
            string email = "person@example.com";
            string mobileNumber = "+4799999999";
            string nationalIdentityNumber = "29320645032";

            List<Recipient> recipients =
            [
                new Recipient { NationalIdentityNumber = nationalIdentityNumber }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(p => p.GetUserContactPoints(It.Is<List<string>>(s => s.Contains(nationalIdentityNumber))))
                .ReturnsAsync(
                [
                    new UserContactPoints
                    {
                        Email = email,
                        IsReserved = true,
                        MobileNumber = mobileNumber,
                        NationalIdentityNumber = nationalIdentityNumber
                    }
                ]);

            var service = GetTestService(profileClient: profileClientMock.Object);

            // Act
            await service.AddEmailAndSmsContactPointsAsync(recipients, null);

            // Assert
            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalIdentityNumber))), Times.Once);

            Assert.True(recipients[0].IsReserved);
            Assert.Equal(2, recipients[0].AddressInfo.Count);
            Assert.Equal(nationalIdentityNumber, recipients[0].NationalIdentityNumber);

            var emailAddress = recipients[0].AddressInfo.OfType<EmailAddressPoint>().FirstOrDefault();
            Assert.NotNull(emailAddress);
            Assert.Equal(email, emailAddress.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddress.AddressType);

            var smsAddress = recipients[0].AddressInfo.OfType<SmsAddressPoint>().FirstOrDefault();
            Assert.NotNull(smsAddress);
            Assert.Equal(mobileNumber, smsAddress.MobileNumber);
            Assert.Equal(AddressType.Sms, smsAddress.AddressType);
        }

        [Fact]
        public async Task AddEmailAndSmsContactPointsAsync_OrganizationNumberAndResource_AuthorizationApplied()
        {
            // Arrange
            string resourceId = "urn:altinn:resource";

            string organizationNumber = "07837399275";
            string officialMobileNumber = "+4748123456";
            string officialEmail = "official@organization.com";

            int authorizedUserId = 200001;
            string authorizedMobileNumber = "+4748123456";
            string authorizedEmail = "authorized-user@domain.com";

            int unauthorizedUserId = 200009;
            string unauthorizedMobileNumber = "+4699999999";
            string unauthorizedEmail = "unauthorized-user@domain.com";

            List<Recipient> recipients =
            [
                new Recipient { OrganizationNumber = organizationNumber }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints
                    {
                        EmailList = [officialEmail],
                        OrganizationNumber = organizationNumber,
                        MobileNumberList = [officialMobileNumber]
                    }
                ]);

            profileClientMock.Setup(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceId))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints
                    {
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new UserContactPoints
                            {
                                Email = authorizedEmail,
                                UserId = authorizedUserId,
                                MobileNumber = authorizedMobileNumber
                            },
                            new UserContactPoints
                            {
                                Email = unauthorizedEmail,
                                UserId = unauthorizedUserId,
                                MobileNumber = unauthorizedMobileNumber
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock.Setup(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceId))))
                .ReturnsAsync((List<OrganizationContactPoints> input, string _) =>
                {
                    input[0].UserContactPoints.RemoveAll(u => u.UserId == unauthorizedUserId);
                    return input;
                });

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailAndSmsContactPointsAsync(recipients, resourceId);

            // Assert
            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceId))), Times.Once);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceId))), Times.Once);

            Assert.Equal(organizationNumber, recipients[0].OrganizationNumber);
            Assert.Equal(4, recipients[0].AddressInfo.Count);

            var emailAddresses = recipients[0].AddressInfo.OfType<EmailAddressPoint>().Select(e => e.EmailAddress).ToList();
            Assert.Contains(officialEmail, emailAddresses);

            var mobileNumbers = recipients[0].AddressInfo.OfType<SmsAddressPoint>().Select(s => s.MobileNumber).ToList();
            Assert.Contains(officialMobileNumber, mobileNumbers);

            Assert.Contains(authorizedEmail, emailAddresses);
            Assert.Contains(authorizedMobileNumber, mobileNumbers);

            Assert.DoesNotContain(unauthorizedEmail, emailAddresses);
            Assert.DoesNotContain(unauthorizedMobileNumber, mobileNumbers);
        }

        private static ContactPointService GetTestService(
            IProfileClient? profileClient = null,
            IAuthorizationService? authorizationService = null)
        {
            if (profileClient == null)
            {
                var profileClientMock = new Mock<IProfileClient>();
                profileClient = profileClientMock.Object;
            }

            if (authorizationService == null)
            {
                var authorizationServiceMock = new Mock<IAuthorizationService>();
                authorizationService = authorizationServiceMock.Object;
            }

            return new ContactPointService(profileClient, authorizationService);
        }
    }
}
