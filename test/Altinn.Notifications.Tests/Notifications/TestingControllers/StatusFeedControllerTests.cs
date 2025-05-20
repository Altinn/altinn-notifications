using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
