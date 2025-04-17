using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class ShipmentDeliveryManifestRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdentifiersToDelete;
    private readonly List<Guid> _ordersChainIdentifiersToDelete;

    public ShipmentDeliveryManifestRepositoryTests()
    {
        _orderIdentifiersToDelete = [];
        _ordersChainIdentifiersToDelete = [];
    }

    public async Task DisposeAsync()
    {
        if (_orderIdentifiersToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdentifiersToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }

        if (_ordersChainIdentifiersToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _ordersChainIdentifiersToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenSmsNotificationOrderExists_ReturnsCorrectShipmentDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string phoneNumber = "+4799999999";
        string senderNumber = "Test Sender";
        string messageBody = "Test SMS message content";
        string senderReference = "SMS-ORDER-REF-30A794B67FE2";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(15);

        // Add order ID to the cleanup list
        _orderIdentifiersToDelete.Add(orderId);

        // Create the notification order
        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.Sms,
            Templates =
            [
                new SmsTemplate(messageBody, senderNumber)
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint(phoneNumber)])
            ]
        };

        // Create corresponding SMS notification
        SmsNotification smsNotification = new()
        {
            OrderId = orderId,
            Id = smsNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = phoneNumber
            },
        };

        // Insert the notification order
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Insert the SMS notification in the database
        SmsNotificationRepository smsNotificationRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsNotificationRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(30), 1);

        // Act
        ShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository = (ShipmentDeliveryManifestRepository)ServiceUtil.GetServices([typeof(IShipmentDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(ShipmentDeliveryManifestRepository));

        INotificationDeliveryManifest? shipmentDeliveryManifest =
            await shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(shipmentDeliveryManifest);

        // Verify basic shipment properties
        Assert.Equal(orderId, shipmentDeliveryManifest.ShipmentId);
        Assert.Equal(senderReference, shipmentDeliveryManifest.SendersReference);
        Assert.Equal("Notification", shipmentDeliveryManifest.Type);
        Assert.NotNull(shipmentDeliveryManifest.Status);
        Assert.NotEmpty(shipmentDeliveryManifest.Status);
        Assert.NotNull(shipmentDeliveryManifest.StatusDescription);
        Assert.True(shipmentDeliveryManifest.LastUpdate > DateTime.MinValue);

        // Verify recipients collection
        Assert.NotNull(shipmentDeliveryManifest.Recipients);
        Assert.Single(shipmentDeliveryManifest.Recipients); // Should have just one SMS delivery

        // Verify it contains an SmsDeliveryManifest
        var smsDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is SmsDeliveryManifest)
            .Cast<SmsDeliveryManifest>()
            .ToList();

        Assert.Single(smsDeliveries);

        // Verify the SMS delivery details
        var smsDelivery = smsDeliveries[0];
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);

        // Verify no Email deliveries exist
        var emailDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is EmailDeliveryManifest)
            .ToList();

        Assert.Empty(emailDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenEmailNotificationOrderExists_ReturnsCorrectShipmentDeliveryManifest()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string emailSubject = "Test Email Subject";
        string emailBody = "Test email body content";
        string senderEmailAddress = "sender@example.com";
        string recipientEmailAddress = "recipient@example.com";
        string senderReference = "EMAIL-ORDER-REF-30A794A67FE1";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(15);

        // Hold the order identifier for cleanup
        _orderIdentifiersToDelete.Add(orderId);

        // Create the notification order
        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            ConditionEndpoint = new Uri("https://vg.no/condition"),
            Templates =
            [
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient([new EmailAddressPoint(recipientEmailAddress)])
            ]
        };

        // Create corresponding email notification
        EmailNotification emailNotification = new()
        {
            OrderId = orderId,
            Id = emailNotificationId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress
            },
        };

        // Insert the notification order
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(notificationOrder);

        // Insert the email notification order
        EmailNotificationRepository emailNotificationRepo = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailNotificationRepo.AddNotification(emailNotification, DateTime.UtcNow);

        // Act
        ShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository = (ShipmentDeliveryManifestRepository)ServiceUtil.GetServices([typeof(IShipmentDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(ShipmentDeliveryManifestRepository));

        INotificationDeliveryManifest? shipmentDeliveryManifest =
            await shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(shipmentDeliveryManifest);

        Assert.Equal(orderId, shipmentDeliveryManifest.ShipmentId);
        Assert.Equal("Notification", shipmentDeliveryManifest.Type);

        Assert.NotNull(shipmentDeliveryManifest.Status);
        Assert.NotEmpty(shipmentDeliveryManifest.Status);

        Assert.NotNull(shipmentDeliveryManifest.StatusDescription);
        Assert.NotEmpty(shipmentDeliveryManifest.StatusDescription);

        Assert.True(shipmentDeliveryManifest.LastUpdate > DateTime.MinValue);
        Assert.Equal(senderReference, shipmentDeliveryManifest.SendersReference);

        Assert.NotNull(shipmentDeliveryManifest.Recipients);
        Assert.Single(shipmentDeliveryManifest.Recipients);

        // Verify it contains an EmailDeliveryManifest
        var emailDeliveries = shipmentDeliveryManifest.Recipients.Where(r => r is EmailDeliveryManifest).Cast<EmailDeliveryManifest>().ToList();
        Assert.Single(emailDeliveries);

        // Verify the email delivery details
        var emailDelivery = emailDeliveries[0];

        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);

        // Verify no SMS deliveries exist
        var smsDeliveries = shipmentDeliveryManifest.Recipients.Where(r => r is SmsDeliveryManifest).ToList();
        Assert.Empty(smsDeliveries);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenNotificationOrderWithSmsPreferred_ReturnsBothSmsAndEmailDeliveryManifests()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string senderReference = "SMS-PREFERRED-ORDER-REF-8D217A";

        // Email details
        string emailSubject = "Secondary Email Subject for SMS Preferred";
        string emailBody = "Secondary email body content for SMS preferred test";
        string senderEmailAddress = "secondary-sender@example.com";
        string recipientEmailAddress = "secondary-recipient@example.com";

        // SMS details
        string smsBody = "Primary SMS message for SMS preferred test";
        string smsSender = "Primary SMS";
        string phoneNumber = "+4777777777";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(25);

        // Add order ID to the cleanup list
        _orderIdentifiersToDelete.Add(orderId);

        // Create the notification order with SmsPreferred channel
        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.SmsPreferred, // Using SmsPreferred
            Templates =
            [
                new SmsTemplate(smsBody, smsSender),
            new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient([
                new SmsAddressPoint(phoneNumber),
                new EmailAddressPoint(recipientEmailAddress)
            ])
            ]
        };

        // Create corresponding email notification
        EmailNotification emailNotification = new(orderId, requestedSendTime)
        {
            Id = emailNotificationId,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress
            }
        };

        // Create corresponding SMS notification
        SmsNotification smsNotification = new()
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = phoneNumber
            }
        };

        // Insert the notification order
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Insert the email notification in the database
        EmailNotificationRepository emailNotificationRepo = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailNotificationRepo.AddNotification(emailNotification, DateTime.UtcNow.AddMinutes(30));

        // Insert the SMS notification in the database
        SmsNotificationRepository smsNotificationRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsNotificationRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(30), 1);

        // Act
        ShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository = (ShipmentDeliveryManifestRepository)ServiceUtil.GetServices([typeof(IShipmentDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(ShipmentDeliveryManifestRepository));

        INotificationDeliveryManifest? shipmentDeliveryManifest =
            await shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(shipmentDeliveryManifest);

        // Verify basic shipment properties
        Assert.Equal(orderId, shipmentDeliveryManifest.ShipmentId);
        Assert.Equal(senderReference, shipmentDeliveryManifest.SendersReference);
        Assert.Equal("Notification", shipmentDeliveryManifest.Type);
        Assert.NotNull(shipmentDeliveryManifest.Status);
        Assert.NotEmpty(shipmentDeliveryManifest.Status);
        Assert.NotNull(shipmentDeliveryManifest.StatusDescription);
        Assert.True(shipmentDeliveryManifest.LastUpdate > DateTime.MinValue);

        // Verify recipients collection - should contain both SMS and email deliveries
        Assert.NotNull(shipmentDeliveryManifest.Recipients);
        Assert.Equal(2, shipmentDeliveryManifest.Recipients.Count); // Should have both SMS and email delivery

        // Verify it contains an SmsDeliveryManifest
        var smsDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is SmsDeliveryManifest)
            .Cast<SmsDeliveryManifest>()
            .ToList();

        Assert.Single(smsDeliveries);

        // Verify the SMS delivery details
        var smsDelivery = smsDeliveries.First();
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);

        // Verify it contains an EmailDeliveryManifest
        var emailDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is EmailDeliveryManifest)
            .Cast<EmailDeliveryManifest>()
            .ToList();

        Assert.Single(emailDeliveries);

        // Verify the email delivery details
        var emailDelivery = emailDeliveries.First();
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);
        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenNotificationOrderWithEmailPreferred_ReturnsBothEmailAndSmsDeliveryManifests()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string senderReference = "PREFERRED-CHANNEL-ORDER-REF-BF725C";

        // Email details
        string emailSubject = "Test Email Subject for Preferred Channel";
        string emailBody = "Test email body content for preferred channel test";
        string senderEmailAddress = "preferred-sender@example.com";
        string recipientEmailAddress = "preferred-recipient@example.com";

        // SMS details
        string smsBody = "Test SMS message for preferred channel";
        string smsSender = "SMS Sender";
        string phoneNumber = "+4788888888";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(20);

        // Add order ID to the cleanup list
        _orderIdentifiersToDelete.Add(orderId);

        // Create the notification order with EmailPreferred channel
        NotificationOrder order = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.EmailPreferred, // Using EmailPreferred
            Templates =
            [
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain),
            new SmsTemplate(smsBody, smsSender)
            ],
            Recipients =
            [
                new Recipient([
                new EmailAddressPoint(recipientEmailAddress),
                new SmsAddressPoint(phoneNumber)
            ])
            ]
        };

        // Create corresponding email notification
        EmailNotification emailNotification = new(orderId, requestedSendTime)
        {
            Id = emailNotificationId,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress
            }
        };

        // Create corresponding SMS notification
        SmsNotification smsNotification = new()
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = phoneNumber
            }
        };

        // Insert the notification order
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(order);

        // Insert the email notification in the database
        EmailNotificationRepository emailNotificationRepo = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailNotificationRepo.AddNotification(emailNotification, DateTime.UtcNow.AddMinutes(30));

        // Insert the SMS notification in the database
        SmsNotificationRepository smsNotificationRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsNotificationRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(30), 1);

        // Act
        ShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository = (ShipmentDeliveryManifestRepository)ServiceUtil.GetServices([typeof(IShipmentDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(ShipmentDeliveryManifestRepository));

        INotificationDeliveryManifest? shipmentDeliveryManifest =
            await shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(shipmentDeliveryManifest);

        // Verify basic shipment properties
        Assert.Equal(orderId, shipmentDeliveryManifest.ShipmentId);
        Assert.Equal(senderReference, shipmentDeliveryManifest.SendersReference);
        Assert.Equal("Notification", shipmentDeliveryManifest.Type);
        Assert.NotNull(shipmentDeliveryManifest.Status);
        Assert.NotEmpty(shipmentDeliveryManifest.Status);
        Assert.NotNull(shipmentDeliveryManifest.StatusDescription);
        Assert.True(shipmentDeliveryManifest.LastUpdate > DateTime.MinValue);

        // Verify recipients collection - should contain both email and SMS deliveries
        Assert.NotNull(shipmentDeliveryManifest.Recipients);
        Assert.Equal(2, shipmentDeliveryManifest.Recipients.Count); // Should have both email and SMS delivery

        // Verify it contains an EmailDeliveryManifest
        var emailDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is EmailDeliveryManifest)
            .Cast<EmailDeliveryManifest>()
            .ToList();

        Assert.Single(emailDeliveries);

        // Verify the email delivery details
        var emailDelivery = emailDeliveries.First();
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);
        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);

        // Verify it contains an SmsDeliveryManifest
        var smsDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is SmsDeliveryManifest)
            .Cast<SmsDeliveryManifest>()
            .ToList();

        Assert.Single(smsDeliveries);

        // Verify the SMS delivery details
        var smsDelivery = smsDeliveries.First();
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WithSmsPreferredChannel_ReturnsBothSmsAndEmailDeliveryManifests()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid smsNotificationId = Guid.NewGuid();
        Guid emailNotificationId = Guid.NewGuid();

        string creator = "TEST_ORG";
        string senderReference = "SMS-PREFERRED-TEST-5421FA3B";

        // SMS details (primary channel)
        string smsBody = "Primary SMS message for SmsPreferred test";
        string smsSender = "SMS Sender";
        string phoneNumber = "+4788776655";

        // Email details (fallback channel)
        string emailSubject = "Fallback Email Subject";
        string emailBody = "Fallback email content for SmsPreferred channel test";
        string senderEmailAddress = "fallback@example.com";
        string recipientEmailAddress = "recipient@example.com";

        DateTime creationDateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(15);

        // Add order ID to the cleanup list
        _orderIdentifiersToDelete.Add(orderId);

        // Create NotificationOrderChainRequest using builder with SmsPreferred channel
        var orderChainRequest = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(Guid.NewGuid()) // We don't need to track this since it's not a chain with reminders
            .SetCreator(new Creator(creator))
            .SetRequestedSendTime(requestedSendTime)
            .SetIdempotencyId("SMSPREF-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 8))
            .SetSendersReference(senderReference)
            .SetRecipient(new NotificationRecipient
            {
                // Use RecipientPerson to access the SmsPreferred channel setting
                RecipientPerson = new RecipientPerson
                {
                    NationalIdentityNumber = "12345678901", // Dummy value for test
                    ChannelSchema = NotificationChannel.SmsPreferred, // Important - specifies SmsPreferred

                    // Primary (SMS) channel settings
                    SmsSettings = new SmsSendingOptions
                    {
                        Body = smsBody,
                        Sender = smsSender,
                        SendingTimePolicy = SendingTimePolicy.Anytime
                    },

                    // Fallback (Email) channel settings
                    EmailSettings = new EmailSendingOptions
                    {
                        Body = emailBody,
                        Subject = emailSubject,
                        SenderEmailAddress = senderEmailAddress,
                        ContentType = EmailContentType.Plain,
                        SendingTimePolicy = SendingTimePolicy.Anytime
                    }
                }
            })
            .Build();

        // Create the notification order with SmsPreferred channel
        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.SmsPreferred, // SmsPreferred channel
            Templates =
            [
                new SmsTemplate(smsBody, smsSender),
                new EmailTemplate(senderEmailAddress, emailSubject, emailBody, EmailContentType.Plain)
            ],
            Recipients =
            [
                new Recipient(
                [
                    new SmsAddressPoint(phoneNumber),
                    new EmailAddressPoint(recipientEmailAddress)
                ],
                null,
                "12345678901")
            ]
        };

        // Create corresponding SMS notification (primary channel)
        SmsNotification smsNotification = new()
        {
            Id = smsNotificationId,
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = phoneNumber,
                NationalIdentityNumber = "12345678901"
            }
        };

        // Create corresponding Email notification (fallback channel)
        EmailNotification emailNotification = new(orderId, requestedSendTime)
        {
            Id = emailNotificationId,
            Recipient = new()
            {
                ToAddress = recipientEmailAddress,
                NationalIdentityNumber = "12345678901"
            }
        };

        // Insert the notification order using the order repository
        OrderRepository orderRepository = (OrderRepository)ServiceUtil.GetServices([typeof(IOrderRepository)])
            .First(i => i.GetType() == typeof(OrderRepository));
        await orderRepository.Create(orderChainRequest, notificationOrder, null);

        // Insert the SMS notification (primary channel)
        SmsNotificationRepository smsNotificationRepository = (SmsNotificationRepository)ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .First(i => i.GetType() == typeof(SmsNotificationRepository));
        await smsNotificationRepository.AddNotification(smsNotification, DateTime.UtcNow.AddMinutes(30), 1);

        // Insert the Email notification (fallback channel)
        EmailNotificationRepository emailNotificationRepository = (EmailNotificationRepository)ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .First(i => i.GetType() == typeof(EmailNotificationRepository));
        await emailNotificationRepository.AddNotification(emailNotification, DateTime.UtcNow.AddMinutes(30));

        // Act
        // Get the delivery manifest to verify both notifications are included
        ShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository = (ShipmentDeliveryManifestRepository)ServiceUtil.GetServices([typeof(IShipmentDeliveryManifestRepository)])
            .First(i => i.GetType() == typeof(ShipmentDeliveryManifestRepository));

        INotificationDeliveryManifest? shipmentDeliveryManifest =
            await shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(orderId, creator, CancellationToken.None);

        // Assert
        Assert.NotNull(shipmentDeliveryManifest);

        // Verify basic shipment properties
        Assert.Equal(orderId, shipmentDeliveryManifest.ShipmentId);
        Assert.Equal(senderReference, shipmentDeliveryManifest.SendersReference);
        Assert.Equal("Notification", shipmentDeliveryManifest.Type);
        Assert.NotNull(shipmentDeliveryManifest.Status);
        Assert.NotEmpty(shipmentDeliveryManifest.Status);
        Assert.NotNull(shipmentDeliveryManifest.StatusDescription);
        Assert.True(shipmentDeliveryManifest.LastUpdate > DateTime.MinValue);

        // Verify recipients collection - should contain both SMS and Email deliveries
        Assert.NotNull(shipmentDeliveryManifest.Recipients);
        Assert.Equal(2, shipmentDeliveryManifest.Recipients.Count); // Should have both SMS and Email deliveries

        // Verify the SMS delivery (primary channel)
        var smsDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is SmsDeliveryManifest)
            .Cast<SmsDeliveryManifest>()
            .ToList();

        Assert.Single(smsDeliveries);

        var smsDelivery = smsDeliveries.First();
        Assert.Equal(phoneNumber, smsDelivery.Destination);
        Assert.NotEmpty(smsDelivery.Status);
        Assert.NotNull(smsDelivery.StatusDescription);
        Assert.True(smsDelivery.LastUpdate > DateTime.MinValue);

        // Verify the Email delivery (fallback channel)
        var emailDeliveries = shipmentDeliveryManifest.Recipients
            .Where(r => r is EmailDeliveryManifest)
            .Cast<EmailDeliveryManifest>()
            .ToList();

        Assert.Single(emailDeliveries);

        var emailDelivery = emailDeliveries.First();
        Assert.Equal(recipientEmailAddress, emailDelivery.Destination);
        Assert.NotEmpty(emailDelivery.Status);
        Assert.NotNull(emailDelivery.StatusDescription);
        Assert.True(emailDelivery.LastUpdate > DateTime.MinValue);

        // Verify order details from both repositories to confirm proper end-to-end processing
        var retrievedOrder = await orderRepository.GetOrderById(orderId, creator);
        Assert.NotNull(retrievedOrder);
        Assert.Equal(NotificationChannel.SmsPreferred, retrievedOrder.NotificationChannel);

        // Verify notifications in both repositories
        var smsRecipients = await smsNotificationRepository.GetRecipients(orderId);
        Assert.NotEmpty(smsRecipients);
        Assert.Equal(phoneNumber, smsRecipients.First().MobileNumber);

        var emailRecipients = await emailNotificationRepository.GetRecipients(orderId);
        Assert.NotEmpty(emailRecipients);
        Assert.Equal(recipientEmailAddress, emailRecipients.First().ToAddress);
    }
}
