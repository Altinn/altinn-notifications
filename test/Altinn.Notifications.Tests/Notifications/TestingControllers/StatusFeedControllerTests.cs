using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Delivery;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            _statusFeedController = new StatusFeedController(_statusFeedService.Object)
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
            var controller = new StatusFeedController(_statusFeedService.Object)
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
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(new List<StatusFeed>());

            // Act
            var result = await _statusFeedController.GetStatusFeed(1);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ActionResult<List<StatusFeedExt>>>(result);
        }

        [Fact]
        public async Task Get_WhenServiceThrowsOperationCanceledException_IsCaughtWithCorrectStatusCodeReturned()
        {
            // Arrange
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>()))
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
            _statusFeedService.Setup(x => x.GetStatusFeed(It.IsAny<int>(), It.IsAny<string>()))
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
