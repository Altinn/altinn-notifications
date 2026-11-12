using System.Collections.Immutable;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Extensions;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Persistence.Mappers;

using Microsoft.Extensions.Caching.Memory;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class NotificationDeliveryManifestServiceTests
{
    private readonly NotificationDeliveryManifestService _service;
    private readonly Mock<INotificationDeliveryManifestRepository> _repositoryMock;
    private readonly IMemoryCache _memoryCache;

    public NotificationDeliveryManifestServiceTests()
    {
        _repositoryMock = new Mock<INotificationDeliveryManifestRepository>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _service = new NotificationDeliveryManifestService(_repositoryMock.Object, _memoryCache);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenManifestExists_ReturnsSuccessResult()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow.AddDays(-10),
            Status = ProcessingLifecycle.SMS_Delivered
        };

        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Destination = "recipient@example.com",
            LastUpdate = DateTime.UtcNow.AddDays(-5),
            Status = ProcessingLifecycle.Email_Delivered
        };

        var expectedManifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            ShipmentId = orderAlternateId,
            LastUpdate = DateTime.UtcNow.AddDays(-5),
            Status = ProcessingLifecycle.Order_Completed,
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
        Assert.False(deliveryManifest.IsProblem);
        Assert.True(deliveryManifest.IsSuccess);
        Assert.NotNull(deliveryManifest.Value);

        var success = deliveryManifest.Value;
        Assert.NotNull(success);
        Assert.Equal(orderAlternateId, success.ShipmentId);

        Assert.Equal("Notification", success.Type);
        Assert.Equal(ProcessingLifecycle.Order_Completed, success.Status);
        Assert.Equal("COMPLETED-NOTIFICATION-ORDER-REF-FCEE4CF15BE1", success.SendersReference);

        Assert.Equal(2, success.Recipients.Count);

        var smsRecipient = success.Recipients[0] as SmsDeliveryManifest;
        Assert.NotNull(smsRecipient);
        Assert.Equal("+4799999999", smsRecipient.Destination);
        Assert.Equal(ProcessingLifecycle.SMS_Delivered, smsRecipient.Status);

        var emailRecipient = success.Recipients[1] as EmailDeliveryManifest;
        Assert.NotNull(emailRecipient);
        Assert.Equal("recipient@example.com", emailRecipient.Destination);
        Assert.Equal(ProcessingLifecycle.Email_Delivered, emailRecipient.Status);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenManifestDoesNotExist_ReturnsErrorResult()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken)).ReturnsAsync((INotificationDeliveryManifest?)null);

        // Act
        var result = await _service.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, cancellationToken);

        // Assert
        Assert.True(result.IsProblem);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Problem);
        Assert.Equal("NOT-00003", result.Problem.ErrorCode.ToString()); // Problems.ShipmentNotFound
        Assert.Equal(404, (int)result.Problem.StatusCode);
        Assert.Equal("Shipment not found", result.Problem.Title);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_VerifyRepositoryIsCalledWithCorrectParameters()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var expectedManifest = CreateManifest(orderAlternateId, ProcessingLifecycle.Order_Completed, ProcessingLifecycle.SMS_Delivered);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
        Assert.True(result.IsProblem);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Problem);
        Assert.Equal("NOT-00003", result.Problem.ErrorCode.ToString()); // Problems.ShipmentNotFound
        Assert.Equal(404, (int)result.Problem.StatusCode);
        Assert.Equal("Shipment not found", result.Problem.Title);
    }

    [Fact]
    public async Task GetDeliveryManifestAsync_WhenRepositoryThrowsException_ThrowsSameException()
    {
        // Arrange
        string orderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
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
    [InlineData(ProcessingLifecycle.SMS_Delivered)]
    [InlineData(ProcessingLifecycle.SMS_SendConditionNotMet)]
    [InlineData(ProcessingLifecycle.Email_SendConditionNotMet)]
    public async Task GetDeliveryManifestAsync_WhenManifestIsTerminal_UsesCacheOnSecondCall(ProcessingLifecycle recipientStatus)
    {
        var (orderAlternateId, orderCreatorName) = await GetManifestTwiceAsync(ProcessingLifecycle.Order_Completed, recipientStatus);

        _repositoryMock.Verify(r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, TestContext.Current.CancellationToken), Times.Once);
    }

    // Guards the terminal status list in the service against drifting from the result types' IsFinal rules.
    [Fact]
    public async Task GetDeliveryManifestAsync_CachesRecipientOnlyWhenItsResultTypeIsFinal()
    {
        var smsStatuses = Enum.GetValues<SmsNotificationResultType>()
            .Select(result => (Name: $"SMS {result}", Final: result.IsFinal(), Status: ProcessingLifecycleMapper.GetSmsLifecycleStage(result.ToString())));
        var emailStatuses = Enum.GetValues<EmailNotificationResultType>()
            .Select(result => (Name: $"Email {result}", Final: result.IsFinal(), Status: ProcessingLifecycleMapper.GetEmailLifecycleStage(result.ToString())));

        foreach (var (name, final, status) in smsStatuses.Concat(emailStatuses))
        {
            var (orderAlternateId, orderCreatorName) = await GetManifestTwiceAsync(ProcessingLifecycle.Order_Completed, status);

            _repositoryMock.Verify(
                r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, TestContext.Current.CancellationToken),
                Times.Exactly(final ? 1 : 2),
                $"{name} should {(final ? string.Empty : "not ")}be cached.");
        }
    }

    [Theory]
    [InlineData(ProcessingLifecycle.Order_Processing, ProcessingLifecycle.SMS_Sending)]
    [InlineData(ProcessingLifecycle.Order_Completed, ProcessingLifecycle.SMS_Sending)]
    public async Task GetDeliveryManifestAsync_WhenOrderOrRecipientIsNotTerminal_DoesNotUseCache(ProcessingLifecycle orderStatus, ProcessingLifecycle recipientStatus)
    {
        var (orderAlternateId, orderCreatorName) = await GetManifestTwiceAsync(orderStatus, recipientStatus);

        _repositoryMock.Verify(r => r.GetDeliveryManifestAsync(orderAlternateId, orderCreatorName, TestContext.Current.CancellationToken), Times.Exactly(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("INVALID-ORG")]
    public async Task GetDeliveryManifestAsync_WithInvalidCreatorName_ReturnsErrorResult(string creatorName)
    {
        // Arrange
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(
                orderAlternateId,
                creatorName,
                cancellationToken))
            .ReturnsAsync((INotificationDeliveryManifest?)null);

        // Act
        var result = await _service.GetDeliveryManifestAsync(orderAlternateId, creatorName, cancellationToken);

        // Assert
        Assert.True(result.IsProblem);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Problem);
        Assert.Equal("NOT-00003", result.Problem.ErrorCode.ToString()); // Problems.ShipmentNotFound
        Assert.Equal(404, (int)result.Problem.StatusCode);
        Assert.Equal("Shipment not found", result.Problem.Title);
    }

    private static NotificationDeliveryManifest CreateManifest(Guid shipmentId, ProcessingLifecycle orderStatus, ProcessingLifecycle recipientStatus)
    {
        return new NotificationDeliveryManifest
        {
            Type = "Notification",
            ShipmentId = shipmentId,
            Status = orderStatus,
            LastUpdate = DateTime.UtcNow.AddDays(-5),
            SendersReference = "NOTIFICATION-ORDER-REF-FCEE4CF15BE1",
            Recipients = ImmutableList.Create<IDeliveryManifest>(new SmsDeliveryManifest
            {
                Destination = "+4799999999",
                LastUpdate = DateTime.UtcNow.AddDays(-10),
                Status = recipientStatus
            })
        };
    }

    private async Task<(Guid OrderAlternateId, string OrderCreatorName)> GetManifestTwiceAsync(ProcessingLifecycle orderStatus, ProcessingLifecycle recipientStatus)
    {
        const string OrderCreatorName = "TEST-ORG";
        Guid orderAlternateId = Guid.NewGuid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var manifest = CreateManifest(orderAlternateId, orderStatus, recipientStatus);
        _repositoryMock.Setup(r => r.GetDeliveryManifestAsync(orderAlternateId, OrderCreatorName, cancellationToken))
            .ReturnsAsync(manifest);

        var firstCall = await _service.GetDeliveryManifestAsync(orderAlternateId, OrderCreatorName, cancellationToken);
        var secondCall = await _service.GetDeliveryManifestAsync(orderAlternateId, OrderCreatorName, cancellationToken);

        Assert.True(firstCall.IsSuccess);
        Assert.True(secondCall.IsSuccess);
        Assert.Equal(manifest, secondCall.Value);

        return (orderAlternateId, OrderCreatorName);
    }
}
