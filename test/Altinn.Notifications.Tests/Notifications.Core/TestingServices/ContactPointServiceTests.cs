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
            string emailAddress = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = true,
                        Email = emailAddress,
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
            Assert.Null(recipient.OrganizationNumber);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            var addressInfo = Assert.Single(recipient.AddressInfo);

            var smsAddressPoint = Assert.IsType<SmsAddressPoint>(addressInfo);
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithEmailAddress()
        {
            // Arrange
            string nationalId = "16219001324";
            string rawMobileNumber = "99999999";
            string emailAddresse = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = true,
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
            await service.AddEmailContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);

            Assert.True(recipient.IsReserved);
            Assert.Null(recipient.OrganizationNumber);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            var addressInfo = Assert.Single(recipient.AddressInfo);

            var emailAddressPoint = Assert.IsType<EmailAddressPoint>(addressInfo);
            Assert.Equal(emailAddresse, emailAddressPoint.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndSmsPreferred_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string nationalId = "17269942983";
            string rawMobileNumber = "99999999";
            string formattedMobileNumber = "+4799999999";
            string emailAddress = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = false,
                        Email = emailAddress,
                        MobileNumber = rawMobileNumber,
                        NationalIdentityNumber = nationalId
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.False(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Single(recipient.AddressInfo);

            var smsAddressPoint = Assert.Single(recipient.AddressInfo.OfType<SmsAddressPoint>());
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndEmailPreferred_ShouldEnrichRecipientsWithEmailAddress()
        {
            // Arrange
            string nationalId = "17269942983";
            string rawMobileNumber = "99999999";
            string emailAddress = "recipient@example.com";

            var recipientsToEnrich = new List<Recipient>
            {
                new() { NationalIdentityNumber = nationalId }
            };

            var profileClientMock = new Mock<IProfileClient>();
            profileClientMock
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
                .ReturnsAsync(
                [
                    new()
                    {
                        UserId = 90090020,
                        IsReserved = true,
                        Email = emailAddress,
                        MobileNumber = rawMobileNumber,
                        NationalIdentityNumber = nationalId
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.True(recipient.IsReserved);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Single(recipient.AddressInfo);

            var emailAddressPoint = Assert.Single(recipient.AddressInfo.OfType<EmailAddressPoint>());
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);
            Assert.Equal(emailAddress, emailAddressPoint.EmailAddress);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
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
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
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
            Assert.Null(recipient.OrganizationNumber);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            Assert.Equal(2, recipient.AddressInfo.Count);

            var smsAddressPoint = Assert.Single(recipient.AddressInfo.OfType<SmsAddressPoint>());
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            var emailAddressPoint = Assert.Single(recipient.AddressInfo.OfType<EmailAddressPoint>());
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);
            Assert.Equal(emailAddresse, emailAddressPoint.EmailAddress);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationRawMobileNumber = "99999999";
            string organizationFirstFormatMobileNumber = "+4799999999";
            string organizationSecondFormatMobileNumber = "004799999999";
            string organizationFirstEmailAddress = "organizatoin-recipient@example.com";

            string contactPersonNationalId = "17269942983";
            string contactPersonRawMobileNumber = "99999999";
            string contactPersonFormattedMobileNumber = "+4799999999";
            string contactPersonEmailAddress = "person-recipient@example.com";

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
                        PartyId = 50660768,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddress, organizationFirstEmailAddress],
                        MobileNumberList = [organizationRawMobileNumber, organizationSecondFormatMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 20020487,
                                IsReserved = false,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
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
            await service.AddSmsContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.IsNotType<EmailAddressPoint>(recipient.AddressInfo);

            var actualMobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(e => e.MobileNumber).ToList();

            Assert.Equal(1, actualMobileNumbers.Count(e => e == contactPersonFormattedMobileNumber));
            Assert.Equal(1, actualMobileNumbers.Count(e => e == organizationFirstFormatMobileNumber));

            Assert.Equal(0, actualMobileNumbers.Count(e => e == organizationRawMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == contactPersonRawMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == organizationSecondFormatMobileNumber));

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithEmailAddresses()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationFormattedMobileNumber = "+4799999999";
            string organizationEmailAddresse = "organization@example.com";

            string contactPersonNationalId = "29326345553";
            string contactPersonFormattedMobileNumber = "+4796666666";
            string contactPersonEmailAddresse = "recipient@example.com";

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
                        PartyId = 50680798,
                        OrganizationNumber = organizationNumber,
                        MobileNumberList = [organizationFormattedMobileNumber, contactPersonFormattedMobileNumber],
                        EmailList = [organizationEmailAddresse, organizationEmailAddresse, organizationEmailAddresse],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 22070487,
                                IsReserved = true,
                                Email = contactPersonEmailAddresse,
                                NationalIdentityNumber = contactPersonNationalId,
                                MobileNumber = contactPersonFormattedMobileNumber
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
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.Equal(2, recipient.AddressInfo.Count);

            var actualEmailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().Select(e => e.EmailAddress).ToList();

            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationEmailAddresse));
            Assert.Equal(1, actualEmailAddresses.Count(e => e == contactPersonEmailAddresse));

            Assert.Equal(0, actualEmailAddresses.Count(e => e == organizationFormattedMobileNumber));
            Assert.Equal(0, actualEmailAddresses.Count(e => e == contactPersonFormattedMobileNumber));

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingOrganizationNumberdAndSmsPreferred_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationRawMobileNumber = "99999999";
            string organizationFormattedMobileNumber = "+4799999999";
            string organizationFirstEmailAddress = "organization@example.com";

            string contactPersonNationalId = "12345678910";
            string contactPersonRawMobileNumber = "95555555";
            string contactPersonFormattedMobileNumber = "+4795555555";
            string contactPersonEmailAddress = "recipient@example.com";

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
                        PartyId = 50301578,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddress, organizationFirstEmailAddress],
                        MobileNumberList = [organizationRawMobileNumber, organizationFormattedMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 20020419,
                                IsReserved = false,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
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
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);
            Assert.Equal(2, recipient.AddressInfo.Count);

            var mobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(s => s.MobileNumber).ToList();
            Assert.Contains(organizationFormattedMobileNumber, mobileNumbers);
            Assert.Contains(contactPersonFormattedMobileNumber, mobileNumbers);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingOrganizationNumberAndEmailPreferred_ShouldEnrichRecipientsWithEmailAddress()
        {
            // Arrange
            string organizationNumber = "987654321";
            string organizationRawMobileNumber = "99999999";
            string organizationFormattedMobileNumber = "+4799999999";
            string organizationFirstEmailAddress = "organization@example.com";
            string organizationSecondEmailAddress = "organization-support@example.com";

            string contactPersonNationalId = "12345678910";
            string contactPersonRawMobileNumber = "95555555";
            string contactPersonEmailAddress = "recipient@example.com";

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
                        PartyId = 50562806,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddress, organizationSecondEmailAddress],
                        MobileNumberList = [organizationRawMobileNumber, organizationFormattedMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 20020693,
                                IsReserved = true,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
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
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.Equal(3, recipient.AddressInfo.Count);

            var actualEmailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().Select(e => e.EmailAddress).ToList();
            Assert.Contains(contactPersonEmailAddress, actualEmailAddresses);
            Assert.Contains(organizationFirstEmailAddress, actualEmailAddresses);
            Assert.Contains(organizationSecondEmailAddress, actualEmailAddresses);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbersAndEmailAddresses()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationFirstRawMobileNumber = "99999999";
            string organizationSecondRawMobileNumber = "96666666";
            string organizationFirstFormattedMobileNumber = "+4799999999";
            string organizationSecondFormattedMobileNumber = "+4796666666";
            string organizationFirstEmailAddresse = "first-organization-email@example.com";
            string organizationSecondEmailAddresse = "second-organization-email@example.com";

            string contactPersonNationalId = "29326345553";
            string contactPersonRawMobileNumber = "95555555";
            string contactPersonFormattedMobileNumber = "+4795555555";
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
                        PartyId = 1532451,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddresse, organizationFirstEmailAddresse, organizationSecondEmailAddresse],
                        MobileNumberList = [organizationFirstRawMobileNumber, organizationSecondRawMobileNumber, organizationSecondRawMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 1522021,
                                IsReserved = false,
                                Email = contactPersonEmailAddresse,
                                MobileNumber = contactPersonRawMobileNumber,
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
            await service.AddEmailAndSmsContactPointsAsync(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            Assert.NotNull(recipient.AddressInfo);
            Assert.Equal(6, recipient.AddressInfo.Count);

            // Assert email addresses
            var emailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().ToList();
            Assert.Equal(3, emailAddresses.Count);
            Assert.Contains(emailAddresses, e => e.EmailAddress == contactPersonEmailAddresse);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationFirstEmailAddresse);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationSecondEmailAddresse);

            // Assert mobile numbers
            var smsAddresses = recipient.AddressInfo.OfType<SmsAddressPoint>().ToList();
            Assert.Equal(3, smsAddresses.Count);
            Assert.Contains(smsAddresses, e => e.MobileNumber == contactPersonFormattedMobileNumber);
            Assert.Contains(smsAddresses, e => e.MobileNumber == organizationFirstFormattedMobileNumber);
            Assert.Contains(smsAddresses, e => e.MobileNumber == organizationSecondFormattedMobileNumber);

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
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
