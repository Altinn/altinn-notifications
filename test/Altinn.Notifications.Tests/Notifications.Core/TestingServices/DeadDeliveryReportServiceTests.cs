using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class DeadDeliveryReportServiceTests
{
    private readonly Mock<IDeadDeliveryReportRepository> _repositoryMock;
    private readonly DeadDeliveryReportService _sut;

    public DeadDeliveryReportServiceTests()
    {
        _repositoryMock = new Mock<IDeadDeliveryReportRepository>();
        _sut = new DeadDeliveryReportService(_repositoryMock.Object);
    }

    [Fact]
    public async Task Insert_WithNullReport_ThrowsArgumentNullException()
    {
        // Arrange
        DeadDeliveryReport? deadDeliveryReport = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.InsertAsync(deadDeliveryReport!, CancellationToken.None));

        Assert.Equal("report", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\n\r   ")]
    public async Task Insert_WithEmptyOrWhiteSpaceReport_ThrowsArgumentException(string? report)
    {
        // Arrange
        var deadDeliveryReport = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow.AddMinutes(5),
            DeliveryReport = report!, 
            Channel = DeliveryReportChannel.AzureCommunicationServices, 
            Resolved = false,
            AttemptCount = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InsertAsync(deadDeliveryReport, CancellationToken.None));

        Assert.Equal("report.DeliveryReport cannot be null or empty (Parameter 'report')", exception.Message);
        Assert.Equal("report", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task Insert_WithInvalidAttemptCount_ThrowsArgumentException(int attemptCount)
    {
        // Arrange
        var deadDeliveryReport = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow.AddMinutes(5),
            DeliveryReport = "{}",
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            Resolved = false,
            AttemptCount = attemptCount
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InsertAsync(deadDeliveryReport, CancellationToken.None));

        Assert.Equal("report.AttemptCount must be greater than zero (Parameter 'report')", exception.Message);
        Assert.Equal("report", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Insert_WithLastAttemptBeforeFirstSeen_ThrowsArgumentException()
    {
        // Arrange
        var firstSeen = DateTime.UtcNow;
        var deadDeliveryReport = new DeadDeliveryReport
        {
            FirstSeen = firstSeen,
            LastAttempt = firstSeen.AddMinutes(-5), // LastAttempt is before FirstSeen
            DeliveryReport = "{}",
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            Resolved = false,
            AttemptCount = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.InsertAsync(deadDeliveryReport, CancellationToken.None));

        Assert.Equal("report.LastAttempt must be greater than or equal to FirstSeen (Parameter 'report')", exception.Message);
        Assert.Equal("report", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.InsertAsync(It.IsAny<DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Insert_WithValidReport_CallsRepositorySuccessfully()
    {
        // Arrange
        var deadDeliveryReport = new DeadDeliveryReport
        {   
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow.AddMinutes(5),
            DeliveryReport = "{}",
            Channel = DeliveryReportChannel.AzureCommunicationServices,
            Resolved = false,
            AttemptCount = 1
        };
        
        _repositoryMock
            .Setup(x => x.InsertAsync(It.IsAny<DeadDeliveryReport>(), CancellationToken.None))
            .Returns(Task.FromResult(1L));

        // Act
        await _sut.InsertAsync(deadDeliveryReport, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            x => x.InsertAsync(
                It.Is<DeadDeliveryReport>(report => report.DeliveryReport == "{}" && report.Channel == DeliveryReportChannel.AzureCommunicationServices),
                CancellationToken.None),
            Times.Once);
    }
}
