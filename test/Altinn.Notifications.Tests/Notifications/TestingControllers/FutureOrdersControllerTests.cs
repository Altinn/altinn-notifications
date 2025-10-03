using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;

public class FutureOrdersControllerTests
{
    private readonly FutureOrdersController _controller;
    private readonly Mock<IOrderRequestService> _orderRequestService;

    public FutureOrdersControllerTests()
    {
        _orderRequestService = new Mock<IOrderRequestService>();
        var validator = new NotificationOrderChainRequestValidator();
        _controller = new FutureOrdersController(_orderRequestService.Object, validator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Items =
                    {
                        ["Org"] = "ttd"
                    }
                }
            }
        };

        // overriding initialization of extension class with test settings
        ResourceLinkExtensions.Initialize("http://localhost:5090");
    }

    [Fact]
    public async Task Post_WithValidRequest_ReturnsCreatedWithOrderResponse()
    {
        // Arrange
        var notificationOrderChainRequest = new NotificationOrderChainRequestExt
        {
            // Initialize properties as needed
            Recipient = new NotificationRecipientExt
            {
                // Initialize properties as needed
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "noreply@digdir.no",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                    }
                }
            },
            IdempotencyId = "test-idempotency-id",
        };

        _orderRequestService.Setup(x => x.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationOrderChainResponse()
            {
                OrderChainId = Guid.NewGuid(),
                OrderChainReceipt = new NotificationOrderChainReceipt
                {
                    ShipmentId = Guid.NewGuid()
                }
            });

        // Act
        var result = await _controller.Post(notificationOrderChainRequest, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task InvalidRequest_ShouldReturn_BadRequest()
    {
        // Arrange
        var invalidRequest = new NotificationOrderChainRequestExt
        {
            // Missing required properties
            IdempotencyId = "test-idempotency-id",
            Recipient = null!
        };

        // Act
        var result = await _controller.Post(invalidRequest, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task ServiceError_ShouldReturn_ProblemDetails()
    {
        // Arrange
        var request = new NotificationOrderChainRequestExt
        {
            Recipient = new NotificationRecipientExt
            {
                RecipientEmail = new RecipientEmailExt
                {
                    EmailAddress = "noreply@digdir.no",
                    Settings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                    }
                }
            },
            IdempotencyId = "test-idempotency-id",
        };

        var serviceError = new ServiceError(
            422,
            "No recipients found");

        _orderRequestService.Setup(x => x.RegisterNotificationOrderChain(It.IsAny<NotificationOrderChainRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceError);

        // Act
        var result = await _controller.Post(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(422, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains(serviceError.ErrorMessage!, problemDetails.Detail);
    }
}
