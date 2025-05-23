using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Delivery;
using Altinn.Notifications.Tests.TestData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers
{
    public class StatusFeedControllerTests
    {
        private readonly Mock<IStatusFeedService> _statusFeedService;
        private readonly StatusFeedController _statusFeedController;

        public StatusFeedControllerTests()
        {
            _statusFeedService = new Mock<IStatusFeedService>();
            _statusFeedController = new StatusFeedController(_statusFeedService.Object, NullLogger<StatusFeedController>.Instance)
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
        }

        [Fact]
        public async Task Get_WithNoOrgSetInHttpContext_ReturnsForbidden()
        {
            // Arrange
            var controller = new StatusFeedController(_statusFeedService.Object, NullLogger<StatusFeedController>.Instance)
            {
                ControllerContext =
                {
                    HttpContext = new DefaultHttpContext()
                },
            };

            // Act
            var result = await controller.GetStatusFeed(1);

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
                new() 
                {
                    SequenceNumber = expectedSequenceNumber,
                    OrderStatus = TestDataConstants.OrderStatusFeedTestOrderCompleted,
                }
            };
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), expectedCreatorName, CancellationToken.None))
                .ReturnsAsync(statusFeedList);

            // Act
            var result = await _statusFeedController.GetStatusFeed(expectedSequenceNumber);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ActionResult<List<StatusFeedExt>>>(result);
            _statusFeedService.Verify(x => x.GetStatusFeed(expectedSequenceNumber, expectedCreatorName, CancellationToken.None), Times.Once);
            var ojectResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedItems = Assert.IsType<List<StatusFeedExt>>(ojectResult.Value);
            Assert.Equal(statusFeedList.Count, returnedItems.Count);
        }

        [Fact]
        public async Task Get_WhenServiceThrowsOperationCanceledException_IsCaughtWithCorrectStatusCodeReturned()
        {
            // Arrange
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), CancellationToken.None))
                .ThrowsAsync(new OperationCanceledException());
            
            // Act
            var result = await _statusFeedController.GetStatusFeed(1);
            
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
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(error);
            
            // Act
            var result = await _statusFeedController.GetStatusFeed(1);
            
            // Assert
            Assert.NotNull(result);
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, statusCodeResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(statusCodeResult.Value);
            Assert.Equal("Bad request", problemDetails.Detail);
        }
    }
}
