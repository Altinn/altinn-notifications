using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class NotificationDeliveryManifestServiceTests
{
    private readonly NotificationDeliveryManifestService _service;
    private readonly Mock<INotificationDeliveryManifestRepository> _repositoryMock;

    public NotificationDeliveryManifestServiceTests()
    {
        _repositoryMock = new Mock<INotificationDeliveryManifestRepository>();
        _service = new NotificationDeliveryManifestService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenManifestExists_ReturnsSuccessResult()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = CancellationToken.None;

        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Status = "Delivered",
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow.AddDays(-10)
        };

        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Status = "Delivered",
            Destination = "recipient@example.com",
            LastUpdate = DateTime.UtcNow.AddDays(-5)
        };

        var expectedManifest = new NotificationDeliveryManifest
        {
            Status = "Completed",
            Type = "Notification",
            ShipmentId = orderAlternateId,
            LastUpdate = DateTime.UtcNow.AddDays(-5),
            SendersReference = "COMPLETED-NOTIFICATION-ORDER-REF-FCEE4CF15BE1",
            Recipients = ImmutableList.Create<IDeliveryManifest>(smsDeliveryManifest, emailDeliveryManifest)
        };

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                orderAlternateId,
                orderCreatorName,
                cancellationToken))
            .ReturnsAsync(expectedManifest);

        // Act
        var deliveryManifest = await _service.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken);

        // Assert
        Assert.False(deliveryManifest.IsError);
        Assert.True(deliveryManifest.IsSuccess);

        deliveryManifest.Match(
            success =>
            {
                Assert.NotNull(success);
                Assert.Equal(orderAlternateId, success.ShipmentId);

                Assert.Null(success.SequenceNumber);
                Assert.Equal("Completed", success.Status);
                Assert.Equal("Notification", success.Type);
                Assert.Equal("COMPLETED-NOTIFICATION-ORDER-REF-FCEE4CF15BE1", success.SendersReference);

                Assert.Equal(2, success.Recipients.Count);

                var smsRecipient = success.Recipients[0] as SmsDeliveryManifest;
                Assert.NotNull(smsRecipient);
                Assert.Equal("Delivered", smsRecipient.Status);
                Assert.Equal("+4799999999", smsRecipient.Destination);

                var emailRecipient = success.Recipients[1] as EmailDeliveryManifest;
                Assert.NotNull(emailRecipient);
                Assert.Equal("Delivered", emailRecipient.Status);
                Assert.Equal("recipient@example.com", emailRecipient.Destination);

                return true;
            },
            error =>
            {
                return false;
            });
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenManifestDoesNotExist_ReturnsErrorResult()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = CancellationToken.None;

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken)).ReturnsAsync((INotificationDeliveryManifest?)null);

        // Act
        var result = await _service.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);

        result.Match(
            success =>
            {
                return false;
            },
            actuallError =>
            {
                Assert.IsType<ServiceError>(actuallError);
                Assert.Equal(404, actuallError.ErrorCode);
                Assert.Equal("Shipment not found.", actuallError.ErrorMessage);

                return true;
            });
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_VerifyRepositoryIsCalledWithCorrectParameters()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = CancellationToken.None;

        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Status = "Delivered",
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow.AddDays(-10)
        };

        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Status = "Delivered",
            Destination = "recipient@example.com",
            LastUpdate = DateTime.UtcNow.AddDays(-5)
        };

        var expectedManifest = new NotificationDeliveryManifest
        {
            Status = "Completed",
            Type = "Notification",
            ShipmentId = orderAlternateId,
            LastUpdate = DateTime.UtcNow.AddDays(-5),
            SendersReference = "COMPLETED-NOTIFICATION-ORDER-REF-FCEE4CF15BE1",
            Recipients = ImmutableList.Create<IDeliveryManifest>(smsDeliveryManifest, emailDeliveryManifest)
        };

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                orderAlternateId,
                orderCreatorName,
                cancellationToken))
            .ReturnsAsync(expectedManifest)
            .Verifiable();

        // Act
        await _service.GetDeliveryManifestAsync(
            orderAlternateId,
            orderCreatorName,
            cancellationToken);

        // Assert
        _repositoryMock.Verify(r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WithInvalidAlternateIdentifier_ReturnsErrorResult()
    {
        // Arrange
        var invalidAlternateId = Guid.Empty;
        string orderCreatorName = "TEST-ORG";
        CancellationToken cancellationToken = CancellationToken.None;

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                invalidAlternateId,
                orderCreatorName,
                cancellationToken))
            .ReturnsAsync((INotificationDeliveryManifest?)null);

        // Act
        var result = await _service.GetDeliveryManifestAsync(
            invalidAlternateId,
            orderCreatorName,
            cancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);

        result.Match(
            success =>
            {
                return false;
            },
            actuallError =>
            {
                Assert.IsType<ServiceError>(actuallError);
                Assert.Equal(404, actuallError.ErrorCode);
                Assert.Equal("Shipment not found.", actuallError.ErrorMessage);

                return true;
            });
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenRepositoryThrowsException_ThrowsSameException()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = CancellationToken.None;
        var expectedException = new InvalidOperationException("Test repository exception");

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                orderAlternateId,
                orderCreatorName,
                cancellationToken))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken));

        Assert.Equal(expectedException.Message, exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INVALID-ORG")]
    public async Task GetDeliveryManifestAsync_WithInvalidCreatorName_ReturnsErrorResult(string creatorName)
    {
        // Arrange
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = CancellationToken.None;

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                orderAlternateId,
                creatorName,
                cancellationToken))
            .ReturnsAsync((INotificationDeliveryManifest?)null);

        // Act
        var result = await _service.GetDeliveryManifestAsync(orderAlternateId, creatorName, cancellationToken);

        // Assert
        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);

        result.Match(
            success =>
            {
                return false;
            },
            actuallError =>
            {
                Assert.IsType<ServiceError>(actuallError);
                Assert.Equal(404, actuallError.ErrorCode);
                Assert.Equal("Shipment not found.", actuallError.ErrorMessage);

                return true;
            });
    }
}
