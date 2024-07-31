using System.Collections.Generic;
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
        /// <summary>
        /// Scenario: 1 recipient with email, 1 recipient with mobile, 1 reserved alid recipient, 1 recipient without contact point
        /// Expected; Email service is called with three recipients, SMS service is called with one recipient
        /// </summary>
        /// Assertion logic is mostly within the setup of the mocked functions in the processing services</remarks>
        [Fact]
        public async Task ProcessOrder_EmailPreferredChannel_WithoutRetry()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients = [
                    new Recipient() { NationalIdentityNumber = "1" },
                    new Recipient() { NationalIdentityNumber = "2" },
                    new Recipient() { NationalIdentityNumber = "3" },
                    new Recipient() { NationalIdentityNumber = "4" }]
            };

            Mock<IEmailOrderProcessingService> emailProcessingMock = new();
            emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "1"))));

            Mock<ISmsOrderProcessingService> smsProcessingMock = new();
            smsProcessingMock
               .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "1"))));

            Mock<IContactPointService> contactPointMock = new();
            contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        if (recipient.NationalIdentityNumber == "1")
                        {
                            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "2")
                        {
                            recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "3")
                        {
                            recipient.IsReserved = true;
                        }
                        else if (recipient.NationalIdentityNumber == "4")
                        {
                            recipient.IsReserved = false;
                        }
                    }
                });

            var preferredChannelProcessingService = new PreferredChannelProcessingService(
                             emailProcessingMock.Object,
                             smsProcessingMock.Object,
                             contactPointMock.Object);

            // Act
            await preferredChannelProcessingService.ProcessOrder(order);

            // Assert
            emailProcessingMock.VerifyAll();
            smsProcessingMock.VerifyAll();
        }

        /// <summary>
        /// Scenario: 1 recipient with email, 1 recipient with mobile, 1 reserved alid recipient, 1 recipient without contact point
        /// Expected; Email service is called with three recipients, SMS service is called with one recipient
        /// </summary>
        /// Assertion logic is mostly within the setup of the mocked functions in the processing services</remarks>
        [Fact]
        public async Task ProcessOrder_EmailPreferredChannel_WithRetry()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.EmailPreferred,
                Recipients = [
                    new Recipient() { NationalIdentityNumber = "1" },
                    new Recipient() { NationalIdentityNumber = "2" },
                    new Recipient() { NationalIdentityNumber = "3" },
                    new Recipient() { NationalIdentityNumber = "4" }]
            };

            Mock<IEmailOrderProcessingService> emailProcessingMock = new();
            emailProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "1"))));

            Mock<ISmsOrderProcessingService> smsProcessingMock = new();
            smsProcessingMock
               .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "1"))));

            Mock<IContactPointService> contactPointMock = new();
            contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        if (recipient.NationalIdentityNumber == "1")
                        {
                            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "2")
                        {
                            recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "3")
                        {
                            recipient.IsReserved = true;
                        }
                        else if (recipient.NationalIdentityNumber == "4")
                        {
                            recipient.IsReserved = false;
                        }
                    }
                });

            var preferredChannelProcessingService = new PreferredChannelProcessingService(
                             emailProcessingMock.Object,
                             smsProcessingMock.Object,
                             contactPointMock.Object);

            // Act
            await preferredChannelProcessingService.ProcessOrderRetry(order);

            // Assert
            emailProcessingMock.VerifyAll();
            smsProcessingMock.VerifyAll();
        }

        /// <summary>
        /// Scenario: 1 recipient with email, 1 recipient with mobile, 1 reserved alid recipient, 1 recipient without contact point
        /// Expected; SMS service is called with three recipients, email service is called with one recipient
        /// </summary>
        /// Assertion logic is mostly within the setup of the mocked functions in the processing services</remarks>
       [Fact]
        public async Task ProcessOrder_SmsPreferredChannel_WithoutRetry()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.SmsPreferred,
                Recipients = [
                    new Recipient() { NationalIdentityNumber = "1" },
                    new Recipient() { NationalIdentityNumber = "2" },
                    new Recipient() { NationalIdentityNumber = "3" },
                    new Recipient() { NationalIdentityNumber = "4" }]
            };

            Mock<IEmailOrderProcessingService> emailProcessingMock = new();
            emailProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "2"))));

            Mock<ISmsOrderProcessingService> smsProcessingMock = new();
            smsProcessingMock
                .Setup(x => x.ProcessOrderWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "2"))));

            Mock<IContactPointService> contactPointMock = new();
            contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        if (recipient.NationalIdentityNumber == "1")
                        {
                            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "2")
                        {
                            recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "3")
                        {
                            recipient.IsReserved = true;
                        }
                        else if (recipient.NationalIdentityNumber == "4")
                        {
                            recipient.IsReserved = false;
                        }
                    }
                });

            var preferredChannelProcessingService = new PreferredChannelProcessingService(
                             emailProcessingMock.Object,
                             smsProcessingMock.Object,
                             contactPointMock.Object);

            // Act
            await preferredChannelProcessingService.ProcessOrder(order);

            // Assert
            emailProcessingMock.VerifyAll();
            smsProcessingMock.VerifyAll();
        }

        /// <summary>
        /// Scenario: 1 recipient with email, 1 recipient with mobile, 1 reserved alid recipient, 1 recipient without contact point
        /// Expected; SMS service is called with three recipients, email service is called with one recipient
        /// </summary>
        /// <remarks>
        /// Assertion logic is mostly within the setup of the mocked functions in the processing services</remarks>
        [Fact]
        public async Task ProcessOrder_SmsPreferredChannel_WithRetry()
        {
            // Arrange
            NotificationOrder order = new()
            {
                NotificationChannel = NotificationChannel.SmsPreferred,
                Recipients = [
                    new Recipient() { NationalIdentityNumber = "1" },
                    new Recipient() { NationalIdentityNumber = "2" },
                    new Recipient() { NationalIdentityNumber = "3" },
                    new Recipient() { NationalIdentityNumber = "4" }]
            };

            Mock<IEmailOrderProcessingService> emailProcessingMock = new();
            emailProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 1 && l.Exists(r => r.NationalIdentityNumber == "2"))));
            
            Mock<ISmsOrderProcessingService> smsProcessingMock = new();
            smsProcessingMock
                .Setup(x => x.ProcessOrderRetryWithoutAddressLookup(It.IsAny<NotificationOrder>(), It.Is<List<Recipient>>(l => l.Count == 3 && !l.Exists(r => r.NationalIdentityNumber == "2"))));
            
            Mock<IContactPointService> contactPointMock = new();
            contactPointMock
                .Setup(cp => cp.AddPreferredContactPoints(order.NotificationChannel, It.IsAny<List<Recipient>>(), It.IsAny<string?>()))
                .Callback<NotificationChannel, List<Recipient>, string?>((_, recipients, _) =>
                {
                    foreach (var recipient in recipients)
                    {
                        if (recipient.NationalIdentityNumber == "1")
                        {
                            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "2")
                        {
                            recipient.AddressInfo.Add(new EmailAddressPoint("2@user.com"));
                            recipient.IsReserved = false;
                        }
                        else if (recipient.NationalIdentityNumber == "3")
                        {
                            recipient.IsReserved = true;
                        }
                        else if (recipient.NationalIdentityNumber == "4")
                        {
                            recipient.IsReserved = false;
                        }
                    }
                });

            var preferredChannelProcessingService = new PreferredChannelProcessingService(
                             emailProcessingMock.Object,
                             smsProcessingMock.Object,
                             contactPointMock.Object);

            // Act
            await preferredChannelProcessingService.ProcessOrderRetry(order);

            // Assert
            emailProcessingMock.VerifyAll();
            smsProcessingMock.VerifyAll();
        }
    }
}
