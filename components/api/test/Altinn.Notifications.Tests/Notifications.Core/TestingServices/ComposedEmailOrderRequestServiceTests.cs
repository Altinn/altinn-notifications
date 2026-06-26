using System;
using System.Collections.Generic;
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
    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

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
    public async Task RegisterComposedEmailOrderChain_MapsOrderCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var chainId = Guid.NewGuid();
        NotificationOrder? captured = null;

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, NotificationOrder, List<NotificationOrder>?, CancellationToken>((_, o, _, _) => captured = o)
            .ReturnsAsync((NotificationOrderChainRequest _, NotificationOrder o, List<NotificationOrder>? _, CancellationToken _) =>
                [new NotificationOrder { Id = o.Id, SendersReference = o.SendersReference }]);

        var service = GetTestService(repoMock.Object);
        var request = ValidComposedEmailRequest(orderId, chainId);

        // Act
        await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(OrderType.Composed, captured.Type);
        Assert.Equal(NotificationChannel.Email, captured.NotificationChannel);
        Assert.NotNull(captured.EmailAttachments);
        Assert.Single(captured.EmailAttachments);
        Assert.Equal("contract.pdf", captured.EmailAttachments[0].Filename);
        Assert.Single(captured.Templates);
        Assert.IsType<EmailTemplate>(captured.Templates[0]);
        Assert.Null(captured.Recipients[0].NationalIdentityNumber);
        Assert.Equal("recipient@altinnxyz.no", ((EmailAddressPoint)captured.Recipients[0].AddressInfo[0]).EmailAddress);
    }

    [Fact]
    public async Task RegisterComposedEmailOrderChain_ReturnsResponse_WithCorrectIds()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var chainId = Guid.NewGuid();
        var savedOrderId = Guid.NewGuid();

        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.Create(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<NotificationOrder>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new NotificationOrder { Id = savedOrderId, SendersReference = "ref-001" }]);

        var service = GetTestService(repoMock.Object);
        var request = ValidComposedEmailRequest(orderId, chainId);

        // Act
        var result = await service.RegisterComposedEmailOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(chainId, result.OrderChainId);
        Assert.Equal(savedOrderId, result.OrderChainReceipt.ShipmentId);
        Assert.Equal("ref-001", result.OrderChainReceipt.SendersReference);
        Assert.Null(result.OrderChainReceipt.Reminders);
    }

    private static NotificationOrderChainRequest ValidComposedEmailRequest(Guid orderId, Guid chainId)
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

        return new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(chainId)
            .SetType(OrderType.Composed)
            .SetCreator(new Creator("ttd"))
            .SetIdempotencyId("idempotency-001")
            .SetSendersReference("ref-001")
            .SetRequestedSendTime(DateTime.UtcNow.AddHours(1))
            .SetRecipient(recipient)
            .Build();
    }

    private static ComposedEmailOrderRequestService GetTestService(
        IOrderRepository? orderRepository = null,
        IGuidService? guidService = null,
        IDateTimeService? dateTimeService = null)
    {
        orderRepository ??= Mock.Of<IOrderRepository>();
        guidService ??= Mock.Of<IGuidService>();
        dateTimeService ??= Mock.Of<IDateTimeService>();

        var config = Options.Create(new NotificationConfig
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "Altinn"
        });

        return new ComposedEmailOrderRequestService(orderRepository, guidService, dateTimeService, config);
    }
}
