using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Files;
using Altinn.Notifications.Models.Recipient;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;

public class ComposedEmailOrdersControllerTests
{
    private const string _validSasUrl =
        "https://altinnstorageaccount.blob.core.windows.net/attachments/contract.pdf" +
        "?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&spr=https&sig=fakesignature";

    public ComposedEmailOrdersControllerTests()
    {
        ResourceLinkExtensions.Initialize("http://localhost:5090");
    }

    [Fact]
    public async Task Post_InvalidRequest_ReturnsBadRequest()
    {
        var serviceMock = new Mock<IOrderRequestService>();
        var controller = CreateController(serviceMock.Object, ValidatorThatFails().Object);

        var result = await controller.Post(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<ObjectResult>(result.Result);
        var objectResult = (ObjectResult)result.Result!;
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        serviceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_MissingOrg_ReturnsForbid()
    {
        var serviceMock = new Mock<IOrderRequestService>();
        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object, org: null);

        var result = await controller.Post(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result.Result);
        serviceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_ExistingOrder_ReturnsOkWithExistingOrderDetails()
    {
        var request = ValidRequest();
        var existingResponse = new NotificationOrderChainResponse
        {
            OrderChainId = Guid.NewGuid(),
            OrderChainReceipt = new NotificationOrderChainReceipt { ShipmentId = Guid.NewGuid() }
        };

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingResponse);

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        var result = await controller.Post(request, TestContext.Current.CancellationToken);

        var result200 = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationOrderChainResponseExt>(result200.Value);
        Assert.Equal(existingResponse.OrderChainId, response.OrderChainId);
        serviceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_NewOrder_ReturnsCreatedWithSelfLink()
    {
        var request = ValidRequest();
        var newResponse = new NotificationOrderChainResponse
        {
            OrderChainId = Guid.NewGuid(),
            OrderChainReceipt = new NotificationOrderChainReceipt { ShipmentId = Guid.NewGuid() }
        };
        var expectedUrl = newResponse.OrderChainId.GetSelfLinkFromOrderChainId();

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);
        serviceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newResponse);

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        var result = await controller.Post(request, TestContext.Current.CancellationToken);

        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(expectedUrl, createdResult.Location);
        var response = Assert.IsType<NotificationOrderChainResponseExt>(createdResult.Value);
        Assert.Equal(newResponse.OrderChainId, response.OrderChainId);
    }

    [Fact]
    public async Task Post_ServiceReturnsProblem_Returns422()
    {
        var request = ValidRequest();

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);
        serviceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Problems.MissingContactInformation);

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        var result = await controller.Post(request, TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problemDetails = Assert.IsType<AltinnProblemDetails>(objectResult.Value);
        Assert.Equal(422, objectResult.StatusCode);
        Assert.Equal("NOT-00001", problemDetails.ErrorCode.ToString());
    }

    [Fact]
    public async Task Post_OperationCanceled_Returns499()
    {
        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        var result = await controller.Post(ValidRequest(), TestContext.Current.CancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problemDetails = Assert.IsType<AltinnProblemDetails>(objectResult.Value);
        Assert.Equal(499, objectResult.StatusCode);
        Assert.Equal("NOT-00002", problemDetails.ErrorCode.ToString());
    }

    [Fact]
    public async Task Post_CancellationTokenForwardedToAllServiceMethods()
    {
        var request = ValidRequest();
        var cancellationToken = TestContext.Current.CancellationToken;

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), cancellationToken))
            .ReturnsAsync((NotificationOrderChainResponse?)null)
            .Verifiable();
        serviceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), cancellationToken))
            .ReturnsAsync(new NotificationOrderChainResponse
            {
                OrderChainId = Guid.NewGuid(),
                OrderChainReceipt = new NotificationOrderChainReceipt { ShipmentId = Guid.NewGuid() }
            })
            .Verifiable();

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        await controller.Post(request, cancellationToken);

        serviceMock.Verify(s => s.RetrieveOrderChainTracking(It.IsAny<string>(), It.IsAny<string>(), cancellationToken), Times.Once);
        serviceMock.Verify(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Post_MapsAttachmentsAndOrderTypeCorrectly()
    {
        var request = ValidRequest();
        NotificationOrderChainRequest? captured = null;

        var serviceMock = new Mock<IOrderRequestService>();
        serviceMock.Setup(s => s.RetrieveOrderChainTracking("ttd", request.IdempotencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderChainResponse?)null);
        serviceMock.Setup(s => s.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderChainRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new NotificationOrderChainResponse
            {
                OrderChainId = Guid.NewGuid(),
                OrderChainReceipt = new NotificationOrderChainReceipt { ShipmentId = Guid.NewGuid() }
            });

        var controller = CreateController(serviceMock.Object, ValidatorThatPasses().Object);

        await controller.Post(request, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("ttd", captured.Creator.ShortName);
        Assert.Equal(request.IdempotencyId, captured.IdempotencyId);
        Assert.Equal(OrderType.ComposedEmail, captured.Type);
        Assert.NotNull(captured.Recipient.RecipientComposedEmail);
        Assert.NotNull(captured.Recipient.RecipientComposedEmail.Settings.Attachments);
        Assert.Single(captured.Recipient.RecipientComposedEmail.Settings.Attachments);
        Assert.Equal("contract.pdf", captured.Recipient.RecipientComposedEmail.Settings.Attachments[0].Filename);
    }

    private static ComposedEmailRequestExt ValidRequest() => new()
    {
        IdempotencyId = "order-001",
        SendersReference = "ref-001",
        RequestedSendTime = DateTime.UtcNow.AddHours(1),
        Recipient = new RecipientComposedEmailExt
        {
            EmailAddress = "recipient@agency.no",
            Settings = new ComposedEmailSendingOptionsExt
            {
                Subject = "Decision from Altinn",
                Body = "Please see the attached document.",
                Attachments =
                [
                    new SasFileReferenceExt
                    {
                        Filename = "contract.pdf",
                        MimeType = "application/pdf",
                        SasUrl = _validSasUrl
                    }
                ]
            }
        }
    };

    private static ComposedEmailOrdersController CreateController(
        IOrderRequestService orderRequestService,
        IValidator<ComposedEmailRequestExt> validator,
        string? org = "ttd")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = org;

        return new ComposedEmailOrdersController(orderRequestService, validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static Mock<IValidator<ComposedEmailRequestExt>> ValidatorThatPasses()
    {
        var mock = new Mock<IValidator<ComposedEmailRequestExt>>();
        mock.Setup(v => v.Validate(It.IsAny<ComposedEmailRequestExt>()))
            .Returns(new ValidationResult());
        return mock;
    }

    private static Mock<IValidator<ComposedEmailRequestExt>> ValidatorThatFails()
    {
        var mock = new Mock<IValidator<ComposedEmailRequestExt>>();
        mock.Setup(v => v.Validate(It.IsAny<ComposedEmailRequestExt>()))
            .Returns(new ValidationResult([new ValidationFailure("SasUrl", "Attachment sasUrl must be an absolute HTTPS URI.")]));
        return mock;
    }
}
