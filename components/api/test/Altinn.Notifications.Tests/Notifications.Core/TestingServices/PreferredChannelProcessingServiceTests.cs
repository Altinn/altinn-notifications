using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class PreferredChannelProcessingServiceTests
    {
        private readonly Mock<IContactPointService> _contactPointMock = new();
        private readonly Mock<ISmsOrderProcessingService> _smsProcessingMock = new();
        private readonly Mock<IEmailOrderProcessingService> _emailProcessingMock = new();

        /// <summary>
        /// Scenario: ProcessOrderRetry is called with EmailPreferred channel
        /// Expected: Retry methods are invoked instead of regular processing methods
        /// </summary>
        [Fact]
        public async Task ProcessOrderRetry_EmailPreferred_CallsRetryMethods()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient { NationalIdentityNumber = "19100047414" },
                    new Recipient { NationalIdentityNumber = "22012047278" },
                    new Recipient { NationalIdentityNumber = "15075848827" },
                    new Recipient { NationalIdentityNumber = "30060136196" }
                ]
            };

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        switch (recipient.NationalIdentityNumber)
                        {
                            case "19100047414":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                                break;
                            case "22012047278":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new EmailAddressPoint("recipient@altinn.xyz"));
                                break;
                            case "15075848827":
                                recipient.IsReserved = true;
                                break;
                            case "30060136196":
                                recipient.IsReserved = false;
                                break;
                        }
                    }
                })
                .Returns(Task.CompletedTask);

            _emailProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "19100047414"))))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "19100047414"))))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(_emailProcessingMock.Object, _smsProcessingMock.Object, _contactPointMock.Object);

            // Act
            await service.ProcessOrderRetry(order);

            // Assert
            _smsProcessingMock.Verify(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Once);
            _emailProcessingMock.Verify(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Once);
        }

        /// <summary>
        /// Scenario: 4 recipients - one gets SMS, one gets email only, one is reserved, one has no contact point
        /// Expected: Recipient with only email goes to fallback channel (email), others (SMS, reserved, no contact) go to preferred channel (SMS)
        /// </summary>
        [Fact]
        public async Task ProcessOrder_SmsPreferred_RecipientsWithOnlyEmailGoToFallback_OthersGoToPreferredChannel()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.SmsPreferred,
                Recipients =
                [
                    new Recipient { NationalIdentityNumber = "19100047414" },
                    new Recipient { NationalIdentityNumber = "22012047278" },
                    new Recipient { NationalIdentityNumber = "15075848827" },
                    new Recipient { NationalIdentityNumber = "30060136196" }
                ]
            };

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        switch (recipient.NationalIdentityNumber)
                        {
                            case "19100047414":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                                break;
                            case "22012047278":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new EmailAddressPoint("recipient@altinn.xyz"));
                                break;
                            case "15075848827":
                                recipient.IsReserved = true;
                                break;
                            case "30060136196":
                                recipient.IsReserved = false;
                                break;
                        }
                    }
                })
                .Returns(Task.CompletedTask);

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(e => e.Count == 1 && e.Exists(r => r.NationalIdentityNumber == "22012047278"))))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(e => e.Count == 3 && !e.Exists(r => r.NationalIdentityNumber == "22012047278"))))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            _emailProcessingMock.VerifyAll();
            _smsProcessingMock.VerifyAll();
        }

        /// <summary>
        /// Scenario: Recipient has both email and SMS addresses with SmsPreferred channel
        /// Expected: Recipient appears in both preferred (SMS) and fallback (email) lists
        /// </summary>
        [Fact]
        public async Task ProcessOrder_SmsPreferred_RecipientWithBothAddresses_AppearsInPreferredAndFallbackLists()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.SmsPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        OrganizationNumber = "999888788",
                        AddressInfo =
                        [
                            new SmsAddressPoint("+4799999999"),
                            new EmailAddressPoint("recipient@altinn.xyz")
                        ]
                    }
                ]
            };

            List<Recipient>? capturedSmsRecipients = null;
            List<Recipient>? capturedEmailRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedSmsRecipients);
            Assert.NotNull(capturedEmailRecipients);

            Assert.Single(capturedSmsRecipients);
            Assert.Single(capturedEmailRecipients);

            Assert.Single(capturedSmsRecipients[0].AddressInfo);
            Assert.Single(capturedEmailRecipients[0].AddressInfo);

            Assert.Equal("999888788", capturedSmsRecipients[0].OrganizationNumber);
            Assert.Equal("999888788", capturedEmailRecipients[0].OrganizationNumber);

            Assert.Equal(AddressType.Sms, capturedSmsRecipients[0].AddressInfo[0].AddressType);
            Assert.Equal(AddressType.Email, capturedEmailRecipients[0].AddressInfo[0].AddressType);
        }

        /// <summary>
        /// Scenario: 4 recipients - one gets SMS only, one gets email, one is reserved, one has no contact point
        /// Expected: Recipient with only SMS goes to fallback channel (SMS), others (email, reserved, no contact) go to preferred channel (email)
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_RecipientsWithOnlySmsGoToFallback_OthersGoToPreferredChannel()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient { NationalIdentityNumber = "19100047414" },
                    new Recipient { NationalIdentityNumber = "22012047278" },
                    new Recipient { NationalIdentityNumber = "15075848827" },
                    new Recipient { NationalIdentityNumber = "30060136196" }
                ]
            };

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        switch (recipient.NationalIdentityNumber)
                        {
                            case "19100047414":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                                break;
                            case "22012047278":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new EmailAddressPoint("recipient@altinn.xyz"));
                                break;
                            case "15075848827":
                                recipient.IsReserved = true;
                                break;
                            case "30060136196":
                                recipient.IsReserved = false;
                                break;
                        }
                    }
                })
                .Returns(Task.CompletedTask);

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "19100047414"))))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "19100047414"))))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            _smsProcessingMock.VerifyAll();
            _emailProcessingMock.VerifyAll();
        }

        /// <summary>
        /// Scenario: Recipient has both email and SMS addresses with EmailPreferred channel
        /// Expected: Recipient appears in both preferred (email) and fallback (SMS) lists
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_RecipientWithBothAddresses_AppearsInPreferredAndFallbackLists()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        OrganizationNumber = "999888777",
                        AddressInfo =
                        [
                            new SmsAddressPoint("+4799999999"),
                            new EmailAddressPoint("recipient@altinn.xyz")
                        ]
                    }
                ]
            };

            List<Recipient>? capturedSmsRecipients = null;
            List<Recipient>? capturedEmailRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedSmsRecipients);
            Assert.NotNull(capturedEmailRecipients);

            Assert.Single(capturedSmsRecipients);
            Assert.Single(capturedEmailRecipients);

            Assert.Single(capturedSmsRecipients[0].AddressInfo);
            Assert.Single(capturedEmailRecipients[0].AddressInfo);

            Assert.Equal("999888777", capturedSmsRecipients[0].OrganizationNumber);
            Assert.Equal("999888777", capturedEmailRecipients[0].OrganizationNumber);

            Assert.Equal(AddressType.Sms, capturedSmsRecipients[0].AddressInfo[0].AddressType);
            Assert.Equal(AddressType.Email, capturedEmailRecipients[0].AddressInfo[0].AddressType);
        }

        /// <summary>
        /// Scenario: ProcessOrderRetry is called with SmsPreferred channel
        /// Expected: Retry methods are invoked instead of regular processing methods
        /// </summary>
        [Fact]
        public async Task ProcessOrderRetry_SmsPreferred_CallsRetryMethods()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.SmsPreferred,
                Recipients =
                [
                    new Recipient { NationalIdentityNumber = "19100047414" },
                    new Recipient { NationalIdentityNumber = "22012047278" },
                    new Recipient { NationalIdentityNumber = "15075848827" },
                    new Recipient { NationalIdentityNumber = "30060136196" }
                ]
            };

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        switch (recipient.NationalIdentityNumber)
                        {
                            case "19100047414":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                                break;
                            case "22012047278":
                                recipient.IsReserved = false;
                                recipient.AddressInfo.Add(new EmailAddressPoint("recipient@altinn.xyz"));
                                break;
                            case "15075848827":
                                recipient.IsReserved = true;
                                break;
                            case "30060136196":
                                recipient.IsReserved = false;
                                break;
                        }
                    }
                })
                .Returns(Task.CompletedTask);

            _emailProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "22012047278"))))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(
                    It.IsAny<NotificationOrder>(),
                    It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "22012047278"))))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrderRetry(order);

            // Assert
            _smsProcessingMock.Verify(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Once);
            _emailProcessingMock.Verify(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()), Times.Once);
        }

        /// <summary>
        /// Scenario: All recipients already have contact points
        /// Expected: AddPreferredContactPoints is NOT called
        /// </summary>
        [Fact]
        public async Task ProcessOrder_AllRecipientsHaveContactPoints_DoesNotCallContactPointService()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo = [new EmailAddressPoint("user1@altinn.xyz")]
                    },
                    new Recipient
                    {
                        NationalIdentityNumber = "22012047278",
                        AddressInfo = [new SmsAddressPoint("+4799999999")]
                    }
                ]
            };

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            _contactPointMock.Verify(
                cp => cp.AddPreferredContactPoints(It.IsAny<NotificationChannel>(), It.IsAny<List<Recipient>>(), It.IsAny<string?>()),
                Times.Never);
        }

        /// <summary>
        /// Scenario: Some recipients have contact points, some don't
        /// Expected: AddPreferredContactPoints is called only for recipients without contact points
        /// </summary>
        [Fact]
        public async Task ProcessOrder_MixedRecipients_CallsContactPointServiceOnlyForMissingContacts()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                ResourceId = "test-resource",
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo = [new EmailAddressPoint("user1@altinn.xyz")]
                    },
                    new Recipient { NationalIdentityNumber = "22012047278" } // No contact point
                ]
            };

            List<Recipient>? capturedRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(
                    NotificationChannel.EmailPreferred,
                    It.IsAny<List<Recipient>>(),
                    "test-resource"))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    capturedRecipients = recipients;
                    recipients[0].AddressInfo.Add(new EmailAddressPoint("user2@altinn.xyz"));
                })
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedRecipients);
            Assert.Single(capturedRecipients);
            Assert.Equal("22012047278", capturedRecipients[0].NationalIdentityNumber);
        }

        /// <summary>
        /// Scenario: Recipient identified by OrganizationNumber
        /// Expected: Recipient is properly processed and routed
        /// </summary>
        [Fact]
        public async Task ProcessOrder_RecipientWithOrganizationNumber_ProcessesCorrectly()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        OrganizationNumber = "123456789",
                        AddressInfo = [new EmailAddressPoint("org@altinn.xyz")]
                    }
                ]
            };

            List<Recipient>? capturedRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedRecipients);
            Assert.Single(capturedRecipients);
            Assert.Equal("123456789", capturedRecipients[0].OrganizationNumber);
        }

        /// <summary>
        /// Scenario: Recipient identified by ExternalIdentity (self-identified user)
        /// Expected: Recipient is properly processed and routed
        /// </summary>
        [Fact]
        public async Task ProcessOrder_RecipientWithExternalIdentity_ProcessesCorrectly()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        ExternalIdentity = "urn:altinn:person:idporten-email:user@altinn.xyz",
                        AddressInfo = [new EmailAddressPoint("user@altinn.xyz")]
                    }
                ]
            };

            List<Recipient>? capturedRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedRecipients);
            Assert.Single(capturedRecipients);
            Assert.Equal("urn:altinn:person:idporten-email:user@altinn.xyz", capturedRecipients[0].ExternalIdentity);
        }

        /// <summary>
        /// Scenario: Recipient with no identifier (GUID is generated internally)
        /// Expected: Recipient is properly processed without throwing exceptions
        /// </summary>
        [Fact]
        public async Task ProcessOrder_RecipientWithNoIdentifier_ProcessesCorrectly()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        AddressInfo = [new EmailAddressPoint("anonymous@altinn.xyz")]
                    }
                ]
            };

            List<Recipient>? capturedRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedRecipients);
            Assert.Single(capturedRecipients);
        }

        /// <summary>
        /// Scenario: Recipient has multiple email addresses
        /// Expected: All email addresses are included in the preferred channel recipient
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_RecipientWithMultipleEmails_AllEmailsIncluded()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo =
                        [
                            new EmailAddressPoint("primary@altinn.xyz"),
                            new EmailAddressPoint("secondary@altinn.xyz")
                        ]
                    }
                ]
            };

            List<Recipient>? capturedRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedRecipients);
            Assert.Single(capturedRecipients);
            Assert.Equal(2, capturedRecipients[0].AddressInfo.Count);
            Assert.All(capturedRecipients[0].AddressInfo, a => Assert.Equal(AddressType.Email, a.AddressType));
        }

        /// <summary>
        /// Scenario: Recipient has multiple SMS addresses
        /// Expected: All SMS addresses are included in the fallback channel recipient (when EmailPreferred)
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_RecipientWithMultipleSms_AllSmsIncludedInFallback()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo =
                        [
                            new SmsAddressPoint("+4799999991"),
                            new SmsAddressPoint("+4799999992")
                        ]
                    }
                ]
            };

            List<Recipient>? capturedSmsRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedSmsRecipients);
            Assert.Single(capturedSmsRecipients);
            Assert.Equal(2, capturedSmsRecipients[0].AddressInfo.Count);
            Assert.All(capturedSmsRecipients[0].AddressInfo, a => Assert.Equal(AddressType.Sms, a.AddressType));
        }

        /// <summary>
        /// Scenario: Reserved recipient with no contact points
        /// Expected: Reserved recipient goes to preferred channel list (for proper error handling)
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_ReservedRecipientNoContacts_GoesToPreferredChannel()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient { NationalIdentityNumber = "19100047414" }
                ]
            };

            List<Recipient>? capturedEmailRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            _contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(It.IsAny<NotificationChannel>(), It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    recipients[0].IsReserved = true;
                })
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedEmailRecipients);
            Assert.Single(capturedEmailRecipients);
            Assert.True(capturedEmailRecipients[0].IsReserved);
            Assert.Equal("19100047414", capturedEmailRecipients[0].NationalIdentityNumber);
        }

        /// <summary>
        /// Scenario: Order with empty recipients list
        /// Expected: No exceptions thrown, contact point service not called
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmptyRecipientsList_ProcessesWithoutException()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients = []
            };

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            var exception = await Record.ExceptionAsync(() => service.ProcessOrder(order));

            // Assert
            Assert.Null(exception);
            _contactPointMock.Verify(
                cp => cp.AddPreferredContactPoints(It.IsAny<NotificationChannel>(), It.IsAny<List<Recipient>>(), It.IsAny<string?>()),
                Times.Never);
        }

        /// <summary>
        /// Scenario: Recipient with IsReserved flag set and both address types
        /// Expected: IsReserved flag is preserved in the split recipient objects
        /// </summary>
        [Fact]
        public async Task ProcessOrder_EmailPreferred_PreservesIsReservedFlag()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        IsReserved = true,
                        AddressInfo =
                        [
                            new EmailAddressPoint("user@altinn.xyz"),
                            new SmsAddressPoint("+4799999999")
                        ]
                    }
                ]
            };

            List<Recipient>? capturedEmailRecipients = null;
            List<Recipient>? capturedSmsRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedSmsRecipients = recipients)
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedEmailRecipients);
            Assert.NotNull(capturedSmsRecipients);
            Assert.True(capturedEmailRecipients[0].IsReserved);
            Assert.True(capturedSmsRecipients[0].IsReserved);
        }

        /// <summary>
        /// Scenario: Multiple recipients with same NationalIdentityNumber but different addresses
        /// Expected: Recipients are deduplicated by identifier, last recipient wins
        /// </summary>
        [Fact]
        public async Task ProcessOrder_DuplicateNationalIdentityNumber_LastRecipientWins()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients =
                [
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo = [new EmailAddressPoint("first@altinn.xyz")]
                    },
                    new Recipient
                    {
                        NationalIdentityNumber = "19100047414",
                        AddressInfo = [new EmailAddressPoint("second@altinn.xyz")]
                    }
                ]
            };

            List<Recipient>? capturedEmailRecipients = null;

            _emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Callback<NotificationOrder, List<Recipient>>((_, recipients) => capturedEmailRecipients = recipients)
                .Returns(Task.CompletedTask);

            _smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.IsAny<List<Recipient>>()))
                .Returns(Task.CompletedTask);

            var service = new PreferredChannelProcessingService(
                _emailProcessingMock.Object,
                _smsProcessingMock.Object,
                _contactPointMock.Object);

            // Act
            await service.ProcessOrder(order);

            // Assert
            Assert.NotNull(capturedEmailRecipients);
            Assert.Single(capturedEmailRecipients);

            var emailAddress = capturedEmailRecipients[0].AddressInfo.OfType<EmailAddressPoint>().First();
            Assert.Equal("second@altinn.xyz", emailAddress.EmailAddress);
        }
    }
}
