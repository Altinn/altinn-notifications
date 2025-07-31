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
            await service.AddEmailContactPoints(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);

            Assert.True(recipient.IsReserved);
            Assert.Null(recipient.OrganizationNumber);
            Assert.Equal(nationalId, recipient.NationalIdentityNumber);

            var addressInfo = Assert.Single(recipient.AddressInfo);

            var emailAddressPoint = Assert.IsType<EmailAddressPoint>(addressInfo);
            Assert.Equal(emailAddress, emailAddressPoint.EmailAddress);
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithMobileNumbersAndEmailAddresses()
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
            Assert.Equal(emailAddress, emailAddressPoint.EmailAddress);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithMobileNumbersOnlyWhenNoEmailAddressesAvailable()
        {
            // Arrange
            string nationalId = "24212205989";
            string rawMobileNumber = "97777777";
            string formattedMobileNumber = "+4797777777";

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
                        UserId = 20202020,
                        IsReserved = false,
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

            Assert.Single(recipient.AddressInfo);

            var smsAddressPoint = Assert.Single(recipient.AddressInfo.OfType<SmsAddressPoint>());
            Assert.Equal(AddressType.Sms, smsAddressPoint.AddressType);
            Assert.Equal(formattedMobileNumber, smsAddressPoint.MobileNumber);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingNationalId_ShouldEnrichRecipientsWithEmailAddressesOnlyWhenNoMobileNumbersAvailable()
        {
            // Arrange
            string nationalId = "05282945993";
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
                        UserId = 20020488,
                        IsReserved = false,
                        Email = emailAddress,
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

            Assert.Single(recipient.AddressInfo);

            var emailAddressPoint = Assert.Single(recipient.AddressInfo.OfType<EmailAddressPoint>());
            Assert.Equal(AddressType.Email, emailAddressPoint.AddressType);
            Assert.Equal(emailAddress, emailAddressPoint.EmailAddress);

            profileClientMock.Verify(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndSmsPreferred_ShouldEnrichRecipientsWithMobileNumbersOnly()
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
            Assert.Null(recipient.OrganizationNumber);
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
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndEmailPreferred_ShouldEnrichRecipientsWithEmailAddressOnly()
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
            Assert.Null(recipient.OrganizationNumber);
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
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndSmsPreferred_ShouldEnrichRecipientsWithEmailAddressesWhenNoMobileNumbersAvailable()
        {
            // Arrange
            string nationalId = "17269942983";
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
            Assert.Null(recipient.OrganizationNumber);
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
        public async Task AddPreferredContactPoints_WhenUsingNationalIdAndEmailPreferred_ShouldEnrichRecipientsWithMobileNumbersWhenNoEmailAddressesAvailable()
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
                .Setup(e => e.GetUserContactPoints(It.Is<List<string>>(e => e.Contains(nationalId))))
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
            await service.AddPreferredContactPoints(NotificationChannel.EmailPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.True(recipient.IsReserved);
            Assert.Null(recipient.OrganizationNumber);
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
        public async Task AddSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbers()
        {
            // Arrange
            string resourceIdentifier = "urn:altinn:tax-report";

            // Organization details
            string organizationNumber = "984456321";
            string organizationRawMobileNumber = "46583920";
            string organizationFirstFormatMobileNumber = "+4746583920";
            string organizationSecondFormatMobileNumber = "004746583920";
            string organizationFirstEmailAddress = "organization-recipient@example.com";
            string organizationSecondEmailAddress = "organization-support@example.com";

            // Contact person under organization
            string contactPersonNationalId = "13309224560";
            string contactPersonRawMobileNumber = "47682930";
            string contactPersonFormattedMobileNumber = "+4747682930";
            string contactPersonEmailAddress = "general-manager@example.com";

            // Registered contact persons with access to the resource
            string firstContactPersonNationalId = "19263535750";
            string firstContactPersonMobileNumber = "+4745681234";
            string firstContactPersonEmailAddress = "first-contact-person@example.com";

            string secondContactPersonNationalId = "19217405109";
            string secondContactPersonMobileNumber = "004745681235";

            string authorizedContactPersonNationalId = "13303439816";
            string authorizedContactPersonRawMobileNumber = "95681236";
            string authorizedContactPersonFormattedMobileNumber = "+4795681236";
            string authorizedContactPersonEmailAddress = "authorized-contact-person@example.com";

            // Recipient
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
                        EmailList = [organizationFirstEmailAddress, organizationSecondEmailAddress, organizationFirstEmailAddress], // Duplicate
                        MobileNumberList = [organizationRawMobileNumber, organizationSecondFormatMobileNumber, organizationFirstFormatMobileNumber], // Duplicate
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200000,
                                IsReserved = false,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
                                NationalIdentityNumber = contactPersonNationalId
                            }
                        ]
                    }
                ]);

            profileClientMock
                .Setup(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), resourceIdentifier))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 50660768,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                Email = firstContactPersonEmailAddress,
                                MobileNumber = firstContactPersonMobileNumber,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },
                            new()
                            {
                                UserId = 200009,
                                IsReserved = true,
                                Email = firstContactPersonEmailAddress,
                                MobileNumber = secondContactPersonMobileNumber,
                                NationalIdentityNumber = secondContactPersonNationalId
                            },
                            new()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddress,
                                MobileNumber = authorizedContactPersonRawMobileNumber,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 50660768,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddress,
                                MobileNumber = authorizedContactPersonRawMobileNumber,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddSmsContactPoints(recipientsToEnrich, resourceIdentifier);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            // Ensure we only get SMS contact points (not email, etc.)
            Assert.All(recipient.AddressInfo, e => Assert.IsType<SmsAddressPoint>(e));

            var actualMobileNumbers = recipient.AddressInfo
                .OfType<SmsAddressPoint>()
                .Select(e => e.MobileNumber)
                .OrderBy(e => e)
                .ToList();

            // Expected included numbers (after normalization/deduplication)
            var expectedMobileNumbers = new List<string>
            {
                organizationFirstFormatMobileNumber,
                contactPersonFormattedMobileNumber,
                authorizedContactPersonFormattedMobileNumber
            };

            Assert.Equal([.. expectedMobileNumbers.OrderBy(e => e)], actualMobileNumbers);

            // Explicit exclusions for raw/duplicate formats and reserved users
            Assert.DoesNotContain(organizationRawMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(contactPersonRawMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(firstContactPersonMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(secondContactPersonMobileNumber, actualMobileNumbers);
            Assert.DoesNotContain(organizationSecondFormatMobileNumber, actualMobileNumbers);

            Assert.Equal(1, actualMobileNumbers.Count(e => e == contactPersonFormattedMobileNumber));
            Assert.Equal(1, actualMobileNumbers.Count(e => e == organizationFirstFormatMobileNumber));
            Assert.Equal(1, actualMobileNumbers.Count(e => e == authorizedContactPersonFormattedMobileNumber));

            Assert.Equal(0, actualMobileNumbers.Count(e => e == organizationRawMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == contactPersonRawMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == secondContactPersonMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == organizationSecondFormatMobileNumber));
            Assert.Equal(0, actualMobileNumbers.Count(e => e == authorizedContactPersonRawMobileNumber));

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceIdentifier))), Times.Once);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceIdentifier))), Times.Once);

            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithEmailAddresses()
        {
            // Arrange
            string resourceIdentifier = "urn:altinn:tax-report";

            // Organization details
            string organizationNumber = "984456321";
            string organizationRawMobileNumber = "46583920";
            string organizationFirstFormatMobileNumber = "+4746583920";
            string organizationSecondFormatMobileNumber = "004746583920";
            string organizationFirstEmailAddress = "organization-recipient@example.com";
            string organizationSecondEmailAddress = "organization-support@example.com";

            // Contact person under organization
            string contactPersonNationalId = "08312015790";
            string contactPersonRawMobileNumber = "47682930";
            string contactPersonEmailAddress = "general-manager@example.com";

            // Registered contact persons with access to the resource
            string firstContactPersonNationalId = "19263535750";
            string firstContactPersonMobileNumber = "+4745681234";
            string firstContactPersonEmailAddress = "first-contact-person@example.com";

            string secondContactPersonNationalId = "19217405109";
            string secondContactPersonMobileNumber = "004745681235";
            string secondContactPersonEmailAddress = "second-contact-person@example.com";

            string authorizedContactPersonNationalId = "13303439816";
            string authorizedContactPersonRawMobileNumber = "95681236";
            string authorizedContactPersonEmailAddress = "authorized-contact-person@example.com";

            // Recipient
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
                        EmailList = [organizationFirstEmailAddress, organizationSecondEmailAddress, organizationFirstEmailAddress],
                        MobileNumberList = [organizationRawMobileNumber, organizationFirstFormatMobileNumber, organizationSecondFormatMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200000,
                                IsReserved = false,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
                                NationalIdentityNumber = contactPersonNationalId
                            }
                        ]
                    }
                ]);

            profileClientMock
                .Setup(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), resourceIdentifier))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 50660768,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200001,
                                IsReserved = false,
                                Email = firstContactPersonEmailAddress,
                                MobileNumber = firstContactPersonMobileNumber,
                                NationalIdentityNumber = firstContactPersonNationalId
                            },
                            new()
                            {
                                UserId = 200009,
                                IsReserved = true,
                                Email = secondContactPersonEmailAddress,
                                MobileNumber = secondContactPersonMobileNumber,
                                NationalIdentityNumber = secondContactPersonNationalId
                            },
                            new()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddress,
                                MobileNumber = authorizedContactPersonRawMobileNumber,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            authorizationServiceMock
                .Setup(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceIdentifier))))
                .ReturnsAsync(
                [
                    new()
                    {
                        PartyId = 50660768,
                        OrganizationNumber = organizationNumber,
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200011,
                                IsReserved = false,
                                Email = authorizedContactPersonEmailAddress,
                                MobileNumber = authorizedContactPersonRawMobileNumber,
                                NationalIdentityNumber = authorizedContactPersonNationalId
                            }
                        ]
                    }
                ]);

            var service = GetTestService(
                profileClient: profileClientMock.Object,
                authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddEmailContactPoints(recipientsToEnrich, resourceIdentifier);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            // Ensure we only get email contact points (not SMS, etc.)
            Assert.All(recipient.AddressInfo, e => Assert.IsType<EmailAddressPoint>(e));

            var actualEmailAddresses = recipient.AddressInfo
                .OfType<EmailAddressPoint>()
                .Select(e => e.EmailAddress)
                .OrderBy(e => e)
                .ToList();

            var expectedEmailAddresses = new List<string>
            {
                contactPersonEmailAddress,
                organizationFirstEmailAddress,
                organizationSecondEmailAddress,
                authorizedContactPersonEmailAddress
            };

            Assert.Equal([.. expectedEmailAddresses.OrderBy(e => e)], actualEmailAddresses);

            // Explicit exclusions for duplicates and non-authorized/reserved users
            Assert.DoesNotContain(firstContactPersonEmailAddress, actualEmailAddresses);
            Assert.DoesNotContain(secondContactPersonEmailAddress, actualEmailAddresses);

            Assert.Equal(1, actualEmailAddresses.Count(e => e == contactPersonEmailAddress));
            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationFirstEmailAddress));
            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationSecondEmailAddress));
            Assert.Equal(1, actualEmailAddresses.Count(e => e == authorizedContactPersonEmailAddress));

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.Verify(e => e.GetUserRegisteredContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber)), It.Is<string>(e => e.Equals(resourceIdentifier))), Times.Once);
            authorizationServiceMock.Verify(e => e.AuthorizeUserContactPointsForResource(It.IsAny<List<OrganizationContactPoints>>(), It.Is<string>(e => e.Equals(resourceIdentifier))), Times.Once);

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

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithEmailAddressesOnlyWhenNoMobileNumbersAvailable()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationFirstEmailAddresse = "first-organization-email@example.com";
            string organizationSecondEmailAddresse = "second-organization-email@example.com";

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
                        PartyId = 1001,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddresse, organizationSecondEmailAddresse],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200001,
                                IsReserved = false,
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
            await service.AddEmailAndSmsContactPointsAsync(recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            var emailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().ToList();
            Assert.Equal(3, emailAddresses.Count);
            Assert.Contains(emailAddresses, e => e.EmailAddress == contactPersonEmailAddresse);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationFirstEmailAddresse);
            Assert.Contains(emailAddresses, e => e.EmailAddress == organizationSecondEmailAddresse);

            Assert.Empty(recipient.AddressInfo.OfType<SmsAddressPoint>());

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);
            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddEmailAndSmsContactPoints_WhenUsingOrganizationNumber_ShouldEnrichRecipientsWithMobileNumbersOnlyWhenNoEmailAddressesAvailable()
        {
            // Arrange
            string organizationNumber = "987654321";
            string organizationFirstRawMobileNumber = "99999999";
            string organizationSecondRawMobileNumber = "96666666";
            string organizationFirstFormattedMobileNumber = "+4799999999";
            string organizationSecondFormattedMobileNumber = "+4796666666";

            string contactPersonNationalId = "28325649641";
            string contactPersonRawMobileNumber = "93333333";
            string contactPersonFormattedMobileNumber = "+4793333333";

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
                        PartyId = 1002,
                        OrganizationNumber = organizationNumber,
                        MobileNumberList = [organizationFirstRawMobileNumber, organizationSecondRawMobileNumber],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 200002,
                                IsReserved = false,
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

            var smsAddresses = recipient.AddressInfo.OfType<SmsAddressPoint>().ToList();
            Assert.Equal(3, smsAddresses.Count);
            Assert.Contains(smsAddresses, s => s.MobileNumber == contactPersonFormattedMobileNumber);
            Assert.Contains(smsAddresses, s => s.MobileNumber == organizationFirstFormattedMobileNumber);
            Assert.Contains(smsAddresses, s => s.MobileNumber == organizationSecondFormattedMobileNumber);

            Assert.Empty(recipient.AddressInfo.OfType<EmailAddressPoint>());

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
        public async Task AddPreferredContactPoints_WhenUsingOrganizationNumberAndSmsPreferred_ShouldEnrichRecipientsWithMobileNumberWhenNoEmailAddressAvailable()
        {
            // Arrange
            string organizationNumber = "987654321";
            string organizationFirstEmailAddress = "organization@example.com";
            string organizationSecondEmailAddress = "organization-support@example.com";

            string contactPersonNationalId = "98765432100";
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
                        PartyId = 50123765,
                        MobileNumberList = [],
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationFirstEmailAddress, organizationSecondEmailAddress],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 20020674,
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
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Null(recipient.IsReserved);
            Assert.Null(recipient.NationalIdentityNumber);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            var actualMobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(e => e.MobileNumber).ToList();
            Assert.Equal(1, actualMobileNumbers.Count(e => e == contactPersonFormattedMobileNumber));

            var actualEmailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().Select(e => e.EmailAddress).ToList();
            Assert.Equal(2, actualEmailAddresses.Count);
            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationFirstEmailAddress));
            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationSecondEmailAddress));

            profileClientMock.Verify(e => e.GetOrganizationContactPoints(It.Is<List<string>>(e => e.Contains(organizationNumber))), Times.Once);

            profileClientMock.VerifyNoOtherCalls();
            authorizationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AddPreferredContactPoints_WhenUsingOrganizationNumberAndSmsPreferred_ShouldEnrichRecipientsWithEmailAddressWhenNoMobileNumbersAvailable()
        {
            // Arrange
            string organizationNumber = "123456789";
            string organizationEmailAddress = "org@example.com";

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
                        PartyId = 50562806,
                        OrganizationNumber = organizationNumber,
                        EmailList = [organizationEmailAddress, organizationEmailAddress],
                        MobileNumberList = [],
                        UserContactPoints =
                        [
                            new()
                            {
                                UserId = 20020693,
                                IsReserved = false,
                                Email = contactPersonEmailAddress,
                                MobileNumber = contactPersonRawMobileNumber,
                                NationalIdentityNumber = contactPersonNationalId
                            }
                        ]
                    }
                ]);

            var authorizationServiceMock = new Mock<IAuthorizationService>();
            var service = GetTestService(profileClient: profileClientMock.Object, authorizationService: authorizationServiceMock.Object);

            // Act
            await service.AddPreferredContactPoints(NotificationChannel.SmsPreferred, recipientsToEnrich, null);

            // Assert
            var recipient = Assert.Single(recipientsToEnrich);
            Assert.Equal(organizationNumber, recipient.OrganizationNumber);

            var actualMobileNumbers = recipient.AddressInfo.OfType<SmsAddressPoint>().Select(e => e.MobileNumber).ToList();
            Assert.Single(actualMobileNumbers);
            Assert.Equal(1, actualMobileNumbers.Count(e => e == contactPersonFormattedMobileNumber));

            var actualEmailAddresses = recipient.AddressInfo.OfType<EmailAddressPoint>().Select(e => e.EmailAddress).ToList();
            Assert.Single(actualEmailAddresses);
            Assert.Equal(1, actualEmailAddresses.Count(e => e == organizationEmailAddress));

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
