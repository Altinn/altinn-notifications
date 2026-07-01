using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Files;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ComposedEmailOrderRequestServiceTests
{
    private static readonly Uri _validSasUrl = new(
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature");

    [Fact]
    public async Task RetrieveOrderChainTracking_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetComposedOrderChainTracking("ttd", "idempotency-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);

        var service = GetTestService(repoMock.Object);

        // Act
        var result = await service.RetrieveOrderChainTracking("ttd", "idempotency-001", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        repoMock.Verify(r => r.GetComposedOrderChainTracking("ttd", "idempotency-001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveOrderChainTracking_ReturnsResponse_WhenFound()
    {
        // Arrange
        var chainId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var existingResponse = new NotificationOrderChainResponse
        {
            OrderChainId = chainId,
            OrderChainReceipt = new NotificationOrderChainReceipt { ShipmentId = shipmentId }
        };

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetComposedOrderChainTracking("ttd", "idempotency-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingResponse);

        var service = GetTestService(repoMock.Object);

        // Act
        var result = await service.RetrieveOrderChainTracking("ttd", "idempotency-001", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chainId, result.OrderChainId);
        Assert.Equal(shipmentId, result.OrderChainReceipt.ShipmentId);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_PersistsOrderWithCorrectProperties()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid chainId = Guid.NewGuid();
        DateTime currentTime = DateTime.UtcNow;
        DateTime requestedSendTime = currentTime.AddHours(2);

        var request = ValidComposedEmailRequest(orderId, chainId, requestedSendTime, "ref-001");

        var expectedOrder = new NotificationOrder
        {
            Id = orderId,
            Type = OrderType.Composed,
            Created = currentTime,
            Creator = new Creator("ttd"),
            SendersReference = "ref-001",
            RequestedSendTime = requestedSendTime,
            NotificationChannel = NotificationChannel.Email,
            EmailAttachments =
            [
                new SasFileReference { Filename = "contract.pdf", MimeType = "application/pdf", SasUrl = _validSasUrl }
            ],
            Templates = [new EmailTemplate("noreply@altinn.no", "Decision from Altinn", "Please see the attached document.", EmailContentType.Plain)],
            Recipients = [new Recipient([new EmailAddressPoint("recipient@altinnxyz.no")])]
        };

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(
                It.Is<NotificationOrderChainRequest>(e => e.OrderChainId == chainId && e.Type == OrderType.Composed),
                It.Is<NotificationOrder>(o =>
                    o.Id == orderId &&
                    o.Type == OrderType.Composed &&
                    o.NotificationChannel == NotificationChannel.Email &&
                    o.EmailAttachments != null &&
                    o.EmailAttachments.Count == 1 &&
                    o.Recipients.Any(r => r.AddressInfo.OfType<EmailAddressPoint>().Any(ep => ep.EmailAddress == "recipient@altinnxyz.no"))),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) =>
                Assert.Equivalent(expectedOrder, o))
            .ReturnsAsync([new NotificationOrder { Id = orderId, SendersReference = "ref-001" }]);

        var dateTimeMock = new Mock<IDateTimeService>();
        dateTimeMock.Setup(d => d.UtcNow()).Returns(currentTime);

        var service = GetTestService(repoMock.Object, dateTimeMock.Object);

        // Act
        var result = await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chainId, result.OrderChainId);
        Assert.Equal(orderId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal("ref-001", result.OrderChainReceipt.SendersReference);
        Assert.Null(result.OrderChainReceipt.Reminders);

        repoMock.VerifyAll();
        dateTimeMock.Verify(d => d.UtcNow(), Times.Once);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_UsesSenderEmailAddress_WhenProvided()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid chainId = Guid.NewGuid();
        const string customSender = "custom@agency.no";

        var recipient = new NotificationRecipient
        {
            RecipientComposedEmail = new RecipientComposedEmail
            {
                EmailAddress = "recipient@altinnxyz.no",
                Settings = new ComposedEmailSendingOptions
                {
                    Subject = "Subject",
                    Body = "Body",
                    SenderEmailAddress = customSender,
                    Attachments = [new SasFileReference { Filename = "doc.pdf", MimeType = "application/pdf", SasUrl = _validSasUrl }]
                }
            }
        };

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId).SetOrderChainId(chainId).SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd")).SetIdempotencyId("idem-001")
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1)).SetRecipient(recipient).Build();

        NotificationOrder? capturedOrder = null;
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) => capturedOrder = o)
            .ReturnsAsync([new NotificationOrder { Id = orderId }]);

        var service = GetTestService(repoMock.Object);

        // Act
        await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedOrder);
        var template = Assert.IsType<EmailTemplate>(capturedOrder.Templates[0]);
        Assert.Equal(customSender, template.FromAddress);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_UsesDefaultFromAddress_WhenSenderEmailAddressIsEmpty()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid chainId = Guid.NewGuid();

        var recipient = new NotificationRecipient
        {
            RecipientComposedEmail = new RecipientComposedEmail
            {
                EmailAddress = "recipient@altinnxyz.no",
                Settings = new ComposedEmailSendingOptions
                {
                    Subject = "Subject",
                    Body = "Body",
                    SenderEmailAddress = null,
                    Attachments = [new SasFileReference { Filename = "doc.pdf", MimeType = "application/pdf", SasUrl = _validSasUrl }]
                }
            }
        };

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId).SetOrderChainId(chainId).SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd")).SetIdempotencyId("idem-002")
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1)).SetRecipient(recipient).Build();

        NotificationOrder? capturedOrder = null;
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) => capturedOrder = o)
            .ReturnsAsync([new NotificationOrder { Id = orderId }]);

        var service = GetTestService(repoMock.Object);

        // Act
        await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedOrder);
        var template = Assert.IsType<EmailTemplate>(capturedOrder.Templates[0]);
        Assert.Equal("noreply@altinn.no", template.FromAddress);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_WithNullAttachments_SetsEmailAttachmentsToNull()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid chainId = Guid.NewGuid();

        var recipient = new NotificationRecipient
        {
            RecipientComposedEmail = new RecipientComposedEmail
            {
                EmailAddress = "recipient@altinnxyz.no",
                Settings = new ComposedEmailSendingOptions
                {
                    Subject = "Subject",
                    Body = "Body",
                    Attachments = null
                }
            }
        };

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetRecipient(recipient)
            .SetOrderChainId(chainId)
            .SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd"))
            .SetIdempotencyId("idem-null-attach")
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1))
            .Build();

        NotificationOrder? capturedOrder = null;
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) => capturedOrder = o)
            .ReturnsAsync([new NotificationOrder { Id = orderId }]);

        var service = GetTestService(repoMock.Object);

        // Act
        await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedOrder);
        Assert.Null(capturedOrder.EmailAttachments);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_ThrowsInvalidOperationException_WhenRepositoryReturnsEmpty()
    {
        // Arrange
        var request = ValidComposedEmailRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(1));

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = GetTestService(repoMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_ThrowsOperationCanceledException_WhenCanceled()
    {
        // Arrange
        var request = ValidComposedEmailRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(1));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = GetTestService();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RegisterComposedEmailOrderChain(request, cts.Token));
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_WithMultipleFileReferences_PersistsAllAttachments()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();
        Guid chainId = Guid.NewGuid();

        var sasUrl1 = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/decision.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=sig1");
        var sasUrl2 = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/appendix.docx?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=sig2");
        var sasUrl3 = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/evidence.xlsx?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=sig3");

        var recipient = new NotificationRecipient
        {
            RecipientComposedEmail = new RecipientComposedEmail
            {
                EmailAddress = "recipient@altinnxyz.no",
                Settings = new ComposedEmailSendingOptions
                {
                    Subject = "Decision with supporting documents",
                    Body = "Please review all attached documents.",
                    Attachments =
                    [
                        new SasFileReference { Filename = "decision.pdf", MimeType = "application/pdf", SasUrl = sasUrl1 },
                        new SasFileReference { Filename = "appendix.docx", MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", SasUrl = sasUrl2 },
                        new SasFileReference { Filename = "evidence.xlsx", MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", SasUrl = sasUrl3 }
                    ]
                }
            }
        };

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId).SetOrderChainId(chainId).SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd")).SetIdempotencyId("multi-attach-001")
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1)).SetRecipient(recipient).Build();

        NotificationOrder? capturedOrder = null;
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(
                It.IsAny<NotificationOrderChainRequest>(),
                It.Is<NotificationOrder>(o => o.EmailAttachments != null && o.EmailAttachments.Count == 3),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) => capturedOrder = o)
            .ReturnsAsync([new NotificationOrder { Id = orderId }]);

        var service = GetTestService(repoMock.Object);

        // Act
        await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedOrder);
        Assert.NotNull(capturedOrder.EmailAttachments);
        Assert.Equal(3, capturedOrder.EmailAttachments.Count);

        Assert.Equal(sasUrl1, capturedOrder.EmailAttachments[0].SasUrl);
        Assert.Equal("decision.pdf", capturedOrder.EmailAttachments[0].Filename);
        Assert.Equal("application/pdf", capturedOrder.EmailAttachments[0].MimeType);

        Assert.Equal(sasUrl2, capturedOrder.EmailAttachments[1].SasUrl);
        Assert.Equal("appendix.docx", capturedOrder.EmailAttachments[1].Filename);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", capturedOrder.EmailAttachments[1].MimeType);

        Assert.Equal(sasUrl3, capturedOrder.EmailAttachments[2].SasUrl);
        Assert.Equal("evidence.xlsx", capturedOrder.EmailAttachments[2].Filename);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", capturedOrder.EmailAttachments[2].MimeType);

        repoMock.VerifyAll();
    }

    [Fact]
    public void SasFileReference_ToString_DoesNotExposesSasUrl()
    {
        // Arrange
        var sasUrl = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=supersecret");
        var fileReference = new SasFileReference
        {
            Filename = "contract.pdf",
            MimeType = "application/pdf",
            SasUrl = sasUrl
        };

        // Act
        var result = fileReference.ToString();

        // Assert
        Assert.DoesNotContain(sasUrl.AbsoluteUri, result, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result, StringComparison.Ordinal);
        Assert.Contains("contract.pdf", result, StringComparison.Ordinal);
        Assert.Contains("application/pdf", result, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationOrder_WithEmailAttachments_RoundTripsCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var sasUrl = new Uri("https://altinnstorageaccount.blob.core.windows.net/attachments/decision.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fakesig");

        var notificationOrder = new NotificationOrder
        {
            Id = orderId,
            Created = DateTime.UtcNow,
            Type = OrderType.Composed,
            Creator = new Creator("ttd"),
            RequestedSendTime = DateTime.UtcNow.AddHours(1),
            NotificationChannel = NotificationChannel.Email,
            EmailAttachments =
            [
                new SasFileReference { Filename = "decision.pdf", MimeType = "application/pdf", SasUrl = sasUrl }
            ],
            Templates = [new EmailTemplate("noreply@altinn.no", "Subject", "Body", EmailContentType.Plain)],
            Recipients = [new Recipient([new EmailAddressPoint("recipient@altinnxyz.no")])]
        };

        // Act
        var json = notificationOrder.Serialize();
        var deserialized = NotificationOrder.Deserialize(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.EmailAttachments);

        Assert.Single(deserialized.EmailAttachments);
        Assert.Equal(sasUrl, deserialized.EmailAttachments[0].SasUrl);
        Assert.Equal("decision.pdf", deserialized.EmailAttachments[0].Filename);
        Assert.Equal("application/pdf", deserialized.EmailAttachments[0].MimeType);
    }

    private static NotificationOrderChainRequest ValidComposedEmailRequest(Guid orderId, Guid chainId, DateTime requestedSendTime, string? sendersReference = null)
    {
        var recipient = new NotificationRecipient
        {
            RecipientComposedEmail = new RecipientComposedEmail
            {
                EmailAddress = "recipient@altinnxyz.no",
                Settings = new ComposedEmailSendingOptions
                {
                    Subject = "Decision from Altinn",
                    Body = "Please see the attached document.",
                    Attachments =
                    [
                        new SasFileReference
                        {
                            Filename = "contract.pdf",
                            MimeType = "application/pdf",
                            SasUrl = _validSasUrl
                        }
                    ]
                }
            }
        };

        var builder = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(chainId)
            .SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd"))
            .SetIdempotencyId("idempotency-001")
            .SetRequestedSendTime(requestedSendTime)
            .SetRecipient(recipient);

        if (sendersReference != null)
        {
            builder.SetSendersReference(sendersReference);
        }

        return builder.Build();
    }

    private static ComposedEmailOrderRequestService GetTestService(
        IOrderRepository? orderRepository = null,
        IDateTimeService? dateTimeService = null)
    {
        orderRepository ??= Mock.Of<IOrderRepository>();
        dateTimeService ??= Mock.Of<IDateTimeService>();

        var config = Options.Create(new NotificationConfig
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "Altinn"
        });

        return new ComposedEmailOrderRequestService(dateTimeService, orderRepository, config);
    }
}
