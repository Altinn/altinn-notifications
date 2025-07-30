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
        public async Task AddSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithMobileNumber()
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
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.True(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            var addressInfo = Assert.Single(recipient.AddressInfo);
            var smsAddressPoint = Assert.IsType<SmsAddressPoint>(addressInfo);
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))), Times.Once);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithEmailAddress()
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

            var addressInfo = Assert.Single(recipient.AddressInfo);
            var emailAddressPoint = Assert.IsType<EmailAddressPoint>(addressInfo);
            Assert.Equal(emailAddresse, emailAddressPoint.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(ids => ids.Contains(nationalId))), Times.Once);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.IsAny<List<string>>()), Times.Never);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.IsAny<List<string>>(), It.IsAny<string>()), Times.Never);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithMobileNumberAndEmailAddress()
        {
            // Arrange
            string nationalId = "17269942983";
            string rawMobileNumber = "99999999";
            string formattedMobileNumber = "+4799999999";
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
                        IsReserved = false,
                        Email = emailAddresse,
                        MobileNumber = rawMobileNumber,
                        NationalIdentityNumber = nationalId
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddEmailAndSmsContactPointsAsync(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.False(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Equal(2, recipient.AddressInfo.Count);

            var smsAddressPoint = Assert.Single(recipient.AddressInfo.OfType<SmsAddressPoint>());
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            var emailAddressPoint = Assert.Single(recipient.AddressInfo.OfType<EmailAddressPoint>());
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);
            Assert.Equal(emailAddresse, emailAddressPoint.EmailAddress);

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
            string organizationFormattedMobileNumber = "+4799999999";
            string organizationInvalidFormattedMobileNumber = "004799999999";

            string mainContactPersonNationalId = "17269942983";
            string mainContactPersonRawMobileNumber = "99999999";
            string mainContactPersonFormattedMobileNumber = "+4799999999";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 1532951,
                        OrganizationNumber = organizationNumber,
                        MobileNumberList = [organizationRawMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 1522201,
                                MobileNumber = mainContactPersonRawMobileNumber,
                                NationalIdentityNumber = mainContactPersonNationalId
                            }
                        ]
                    }
                ]);

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
            Assert.Single(recipient.AddressInfo);

            var actualMobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(a => a.MobileNumber).ToList();

            Assert.Contains(organizationFormattedMobileNumber, actualMobileNumbers);
            Assert.Contains(mainContactPersonFormattedMobileNumber, actualMobileNumbers);

            Assert.DoesNotContain(organizationRawMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(mainContactPersonRawMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(organizationInvalidFormattedMobileNumber, actualMobileNumbers);

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

            string contactPersonNationalId = "29326345553";
            string contactPersonEmailAddresse = "main-recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var profileClientMock = new Mock<IProfileClient>();

            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 1532951,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationEmailAddresse, organizationEmailAddresse, organizationEmailAddresse],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 1522021,
                                Email = contactPersonEmailAddresse,
                                NationalIdentityNumber = contactPersonNationalId
                            }
                        ]
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
        public async Task AddEmailAndSmsContactPointsAsync_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbersAndEmailAddresses()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationFirstRawMobileNumber = "99999999";
            string organizationSecondRawMobileNumber = "96666666";

            string organizationFirstFormattedMobileNumber = "+4799999999";
            string organizationSecondFormattedMobileNumber = "+4796666666";

            string organizationFirstEmailAddresse = "first-organization-email@example.com";
            string organizationSecondEmailAddresse = "second-organization-email@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { OrganizationNumber = organizationNumber }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 1532451,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddresse, organizationFirstEmailAddresse, organizationSecondEmailAddresse],
                        MobileNumberList = [organizationFirstRawMobileNumber, organizationSecondRawMobileNumber, organizationSecondRawMobileNumber]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddEmailAndSmsContactPointsAsync(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.NotNull(recipient.AddressInfo);
            Assert.Equal(4, recipient.AddressInfo.Count);

            // Extract email and SMS address points
            var emailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().ToList();
            var smsAddresses = recipient.AddressInfo.OfType<SmsAddressPoint>().ToList();

            // Assert emails
            Assert.Equal(2, emailAddresses.Count);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationFirstEmailAddresse);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationSecondEmailAddresse);

            // Assert SMS/mobile numbers
            Assert.Equal(2, smsAddresses.Count);
            Assert.Contains(smsAddresses, e => e.MobileNumber == organizationFirstFormattedMobileNumber);
            Assert.Contains(smsAddresses, e => e.MobileNumber == organizationSecondFormattedMobileNumber);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(orgs => orgs.Contains(organizationNumber))), Times.Once);

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
