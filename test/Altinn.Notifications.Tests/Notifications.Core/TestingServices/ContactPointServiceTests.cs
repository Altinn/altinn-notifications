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
        public async Task AddSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string nationalId = "17269942983";
            string rawMobileNumber = "99999999";
            string formattedMobileNumber = "+4799999999";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = true,
                        MobileNumber = rawMobileNumber,
                        NationalIdentityNumber = nationalId
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddSmsContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = recipientsToEnrich[0];
            Assert.True(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Single(recipient.AddressInfo);
            var smsAddressPoint = Assert.IsType<SmsAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))), Times.Once);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithEmailAddresses()
        {
            // Arrange
            string nationalId = "16219001324";
            string emailAddresse = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = true,
                        Email = emailAddresse,
                        NationalIdentityNumber = nationalId
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.True(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Single(recipient.AddressInfo);
            var emailAddressPoint = Assert.IsType<EmailAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(emailAddresse, emailAddressPoint.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))), Times.Once);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationRawMobileNumber = "99999999";
            string organizationFirstFormatMobileNumber = "+4799999999";
            string organizationSecondFormatMobileNumber = "004799999999";

            string contactPersonFirstFormatMobileNumber = "+4796666666";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var organizationContactPointsMock = new List<OrganizationContactPoints>
            {
                new()
                {
                    OrganizationNumber = organizationNumber,
                    MobileNumberList = [organizationRawMobileNumber, organizationFirstFormatMobileNumber, organizationSecondFormatMobileNumber],
                    UserContactPoints =
                    [
                        new()
                        {
                            UserId = 90090040,
                            IsReserved = false,
                            MobileNumber = "96666666",
                            NationalIdentityNumber = "01325339035"
                        },
                        new()
                        {
                            UserId = 90090070,
                            IsReserved = false,
                            NationalIdentityNumber = "29249014573",
                            MobileNumber = contactPersonFirstFormatMobileNumber
                        },
                        new()
                        {
                            UserId = 90090090,
                            IsReserved = false,
                            MobileNumber = "004796666666",
                            NationalIdentityNumber = "02322015847"
                        },
                        new()
                        {
                            UserId = 90090110,
                            IsReserved = false,
                            NationalIdentityNumber = "12213447880",
                            MobileNumber = organizationSecondFormatMobileNumber
                        }
                    ]
                }
            };

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(organizationContactPointsMock);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddSmsContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.NotNull(recipient.AddressInfo);
            Assert.Equal(2, recipient.AddressInfo.Count);

            var organizationAddressPoint = Assert.IsType<SmsAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(AddressType.Sms, organizationAddressPoint.AddressType);
            Assert.Equal(organizationFirstFormatMobileNumber, organizationAddressPoint.MobileNumber);

            var contactPersonAddressPoint = Assert.IsType<SmsAddressPoint>(recipient.AddressInfo[1]);
            Assert.Equal(AddressType.Sms, contactPersonAddressPoint.AddressType);
            Assert.Equal(contactPersonFirstFormatMobileNumber, contactPersonAddressPoint.MobileNumber);

            var notExpectedMobileNumbers = new[]
            {
                "96666666",
                "004796666666",
                organizationRawMobileNumber,
                organizationSecondFormatMobileNumber
            };
            var actualMobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(a => a.MobileNumber).ToList();
            foreach (var mobileNumber in notExpectedMobileNumbers)
            {
                Assert.DoesNotContain(mobileNumber, actualMobileNumbers);
            }

            profileClientMock.Verify(x => x.GetOrganizationContactPoints(It.Is<List<string>>(orgs => orgs.Contains(organizationNumber))), Times.Once);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithEmailAddresses()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationEmailAddresse = "organization@example.com";

            string contactPersonEmailAddresse = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var organizationContactPointsMock = new List<OrganizationContactPoints>
            {
                new()
                {
                    OrganizationNumber = organizationNumber,
                    EmailList = [organizationEmailAddresse],
                    UserContactPoints =
                    [
                        new()
                        {
                            UserId = 90090040,
                            IsReserved = false,
                            Email = contactPersonEmailAddresse,
                            NationalIdentityNumber = "01325339035"
                        },
                        new()
                        {
                            UserId = 90090070,
                            IsReserved = false,
                            Email = contactPersonEmailAddresse,
                            NationalIdentityNumber = "29249014573"
                        }
                    ]
                }
            };

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(organizationContactPointsMock);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.NotNull(recipient.AddressInfo);
            Assert.Equal(2, recipient.AddressInfo.Count);

            var organizationAddressPoint = Assert.IsType<EmailAddressPoint>(recipient.AddressInfo[0]);
            Assert.Equal(AddressType.Email, organizationAddressPoint.AddressType);
            Assert.Equal(organizationEmailAddresse, organizationAddressPoint.EmailAddress);

            var contactPersonAddressPoint = Assert.IsType<EmailAddressPoint>(recipient.AddressInfo[1]);
            Assert.Equal(AddressType.Email, contactPersonAddressPoint.AddressType);
            Assert.Equal(contactPersonEmailAddresse, contactPersonAddressPoint.EmailAddress);

            profileClientMock.Verify(x => x.GetOrganizationContactPoints(It.Is<List<string>>(orgs => orgs.Contains(organizationNumber))), Times.Once);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddSmsContactPoints_WhenUsingOrganizationNumberAndResourceId_ShouldAuthorizeAndEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string resourceIdentifier = "urn:altinn:resource-id";

            string organizationNumber = "123456789";
            string organizationRawMobileNumber = "99999999";
            string organizationFirstFormatMobileNumber = "+4799999999";
            string organizationSecondFormatMobileNumber = "004799999999";

            string contactPersonRawMobileNumber = "96666666";
            string unauthorizedPersonFormatMobileNumber = "+4797777777";
            string contactPersonFirstFormatMobileNumber = "+4796666666";
            string contactPersonSecondFormatMobileNumber = "004796666666";

            string firstContactPersonNationalId = "03288308712";
            string secondContactPersonNationalId = "08297224086";
            string thirdContactPersonNationalId = "24283830469";
            string fourthContactPersonNationalId = "17275807885";
            string unauthorizedContactPersonNationalId = "17275807885";

            List<Recipient> recipientsToEnrich =
            [
                new Recipient()
                {
                    OrganizationNumber = organizationNumber
                }
            ];

            List<Recipient> enrichedAuthorizedRecipients =
            [
                new Recipient()
                {
                    OrganizationNumber = organizationNumber,

                    AddressInfo =
                    [
                        new SmsAddressPoint(organizationFirstFormatMobileNumber),
                        new SmsAddressPoint(contactPersonFirstFormatMobileNumber)
                    ]
                }
            ];

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        OrganizationNumber = organizationNumber,
                        MobileNumberList =
                        [
                            organizationRawMobileNumber,
                            contactPersonRawMobileNumber,
                            organizationSecondFormatMobileNumber,
                            contactPersonSecondFormatMobileNumber
                        ]
                    }
                ]);

            profileClientMock
                .Setup(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                MobileNumber = organizationRawMobileNumber,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200009,
                                IsReserved = false,
                                MobileNumber = contactPersonRawMobileNumber,
                                NationalIdentityNumber = secondContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                MobileNumber = organizationSecondFormatMobileNumber,
                                NationalIdentityNumber = thirdContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200007,
                                IsReserved = false,
                                MobileNumber = contactPersonSecondFormatMobileNumber,
                                NationalIdentityNumber = fourthContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200015,
                                IsReserved = false,
                                MobileNumber = unauthorizedPersonFormatMobileNumber,
                                NationalIdentityNumber = unauthorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                MobileNumber = organizationRawMobileNumber,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200009,
                                IsReserved = false,
                                MobileNumber = contactPersonRawMobileNumber,
                                NationalIdentityNumber = secondContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                MobileNumber = organizationSecondFormatMobileNumber,
                                NationalIdentityNumber = thirdContactPersonNationalId
                            },

                            new UserContactPoints()
                            {
                                UserId = 200007,
                                IsReserved = false,
                                MobileNumber = contactPersonSecondFormatMobileNumber,
                                NationalIdentityNumber = fourthContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddSmsContactPoints(recipientsToEnrich, resourceIdentifier);

            // Assert 
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(enrichedAuthorizedRecipients, recipientsToEnrich);
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingOrganizationNumberAndResourceId_ShouldAuthorizeAndEnrichRecipientsWithEmailAddresses()
        {
            // Arrange
            string resourceIdentifier = "urn:altinn:resource-id";

            string organizationNumber = "123456789";
            string organizationEmailAddresse = "org@example.com";

            string firstContactPersonNationalId = "04319644518";
            string firstContactPersonEmailAddresse = "contact@example.com";

            string authorizedContactPersonNationalId = "02211608385";
            string authorizedContactPersonEmailAddresse = "authorized@example.com";

            string unauthorizedContactPersonNationalId = "17275807885";
            string unauthorizedContactPersonEmailAddresse = "unauthorized@example.com";

            List<Recipient> recipientsToEnrich =
            [
                new Recipient()
                {
                    OrganizationNumber = organizationNumber
                }
            ];

            List<Recipient> enrichedAuthorizedRecipients =
            [
                new Recipient()
                {
                    OrganizationNumber = organizationNumber,

                    AddressInfo =
                    [
                        new EmailAddressPoint(organizationEmailAddresse),
                        new EmailAddressPoint(firstContactPersonEmailAddresse),
                        new EmailAddressPoint(authorizedContactPersonEmailAddresse)
                    ]
                }
            ];

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = organizationNumber,
                        EmailList =
                        [
                            organizationEmailAddresse,
                            firstContactPersonEmailAddresse
                        ]
                    }
                ]);

            profileClientMock
                .Setup(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                Email = firstContactPersonEmailAddresse,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },
                            new UserContactPoints()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddresse,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            },
                            new UserContactPoints()
                            {
                                UserId = 200013,
                                IsReserved = false,
                                Email = unauthorizedContactPersonEmailAddresse,
                                NationalIdentityNumber = unauthorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(s => s.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new OrganizationContactPoints()
                    {
                        PartyId = 78901,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new UserContactPoints()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                Email = firstContactPersonEmailAddresse,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },
                            new UserContactPoints()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddresse,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var service = GetTestService(profileClientMock.Object, authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(recipientsToEnrich, resourceIdentifier);

            // Assert
            profileClientMock.VerifyAll();
            authorizationServiceMock.VerifyAll();
            Assert.Equivalent(enrichedAuthorizedRecipients, recipientsToEnrich);
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
