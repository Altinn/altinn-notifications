using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class AltinnServiceUpdateServiceTests
{
    private Mock<INotificationsEmailServiceUpdateService> _notificationsEmail;
    private Mock<ILogger<IAltinnServiceUpdateService>> _loggerMock;

    public AltinnServiceUpdateServiceTests()
    {
        _loggerMock = new();
        _notificationsEmail = new();
    }

    [Fact]
    public async Task HandleServiceUpdate_NotificationsEmailSource_IsMatchedToCorrectService()
    {
        // Arrange
        _notificationsEmail = new();
        _notificationsEmail.Setup(s => s.HandleServiceUpdate(
            It.IsAny<AltinnServiceUpdateSchema>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        AltinnServiceUpdateService service = new(_notificationsEmail.Object, _loggerMock.Object);

        // Act
        await service.HandleServiceUpdate("platform-notifications-email", AltinnServiceUpdateSchema.ResourceLimitExceeded, string.Empty);

        // Assert
        _notificationsEmail.VerifyAll();
    }

    [Fact]
    public async Task HandleServiceUpdate_UnknownSource_InformationIsLogged()
    {
        // Arrange
        _loggerMock = new();
        AltinnServiceUpdateService service = new(_notificationsEmail.Object, _loggerMock.Object);

        // Act
        await service.HandleServiceUpdate("platform-unknown-service", AltinnServiceUpdateSchema.ResourceLimitExceeded, string.Empty);

        // Assert
        #pragma warning disable CS8602 // Dereference of a possibly null reference.
        _loggerMock.Verify(
            x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received service from unknown service")),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
       #pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
}
