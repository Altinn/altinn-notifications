using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Status;
using Altinn.Notifications.Validators;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers
{
    public class StatusFeedControllerTests
    {
        private readonly Mock<IStatusFeedService> _statusFeedService;
        private readonly GetStatusFeedRequestValidator _validator = new();
        private readonly StatusFeedController _sut;

        public StatusFeedControllerTests()
        {
            _statusFeedService = new Mock<IStatusFeedService>();
            
            _sut = new StatusFeedController(_statusFeedService.Object, _validator)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        Items = { ["Org"] = "ttd" }
                    }
                }
            };
        }

        [Fact]
        public async Task Get_WithNoOrgSetInHttpContext_ReturnsForbidden()
        {
            // Arrange
            var controller = new StatusFeedController(_statusFeedService.Object, _validator)
            {
                ControllerContext =
                {
                    HttpContext = new DefaultHttpContext()
                },
            };

            // Act
            var result = await controller.GetStatusFeed(new GetStatusFeedRequestExt { Seq = 1 });

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task Get_WithSequenceNumberAndCreatorNameHeader_ReturnsListOfStatusFeedItems()
        {
            // Arrange
            var expectedSequenceNumber = 1;
            var expectedCreatorName = "ttd";
            var statusFeedList = new List<StatusFeed>
            {
                new StatusFeed
                {
                    SequenceNumber = expectedSequenceNumber,
                    OrderStatus = new OrderStatus
                    {
                        Recipients = new List<Recipient>
                            {
                                new Recipient
                                {
                                    Destination = "noreply@altinn.no",
                                    Status = ProcessingLifecycle.Order_Completed,
                                    LastUpdate = DateTime.UtcNow
                                }
                            }.ToImmutableList(),
                        ShipmentId = Guid.NewGuid(),
                        SendersReference = "ref123",
                        LastUpdated = DateTime.UtcNow,
                        ShipmentType = "Notification"
                    }
                }
            };
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<long>(), It.IsAny<int?>(), expectedCreatorName, CancellationToken.None))
                    .ReturnsAsync(statusFeedList);

            // Act
            var result = await _sut.GetStatusFeed(new GetStatusFeedRequestExt { Seq = expectedSequenceNumber });

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ActionResult<List<StatusFeedExt>>>(result);
            _statusFeedService.Verify(x => x.GetStatusFeed(expectedSequenceNumber, It.IsAny<int?>(), expectedCreatorName, CancellationToken.None), Times.Once);
            var ojectResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedItems = Assert.IsType<List<StatusFeedExt>>(ojectResult.Value);
            Assert.Equal(statusFeedList.Count, returnedItems.Count);
            Assert.Equal("Notification", returnedItems[0].ShipmentType);
        }

        [Fact]
        public async Task Get_WhenServiceThrowsOperationCanceledException_IsCaughtWithCorrectStatusCodeReturned()
        {
            // Arrange
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<string>(), CancellationToken.None))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await _sut.GetStatusFeed(new GetStatusFeedRequestExt { Seq = 1 });

            // Assert
            Assert.NotNull(result);
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(499, statusCodeResult.StatusCode);
            Assert.Equal("Request terminated - The client disconnected or cancelled the request before the server could complete processing", statusCodeResult.Value);
        }

        [Fact]
        public async Task Get_WhenServiceReturnsError_CorrectStatusCodeIsReturned()
        {
            // Arrange
            var error = new ServiceError(400, "Bad request");
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(error);

            // Act
            var result = await _sut.GetStatusFeed(new GetStatusFeedRequestExt { Seq = 1 });

            // Assert
            Assert.NotNull(result);
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, statusCodeResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(statusCodeResult.Value);
            Assert.Equal("Bad request", problemDetails.Detail);
        }
    }
}
