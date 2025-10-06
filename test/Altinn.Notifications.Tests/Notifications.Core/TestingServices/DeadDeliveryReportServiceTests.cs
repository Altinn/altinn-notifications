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

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   \t\n\r   ")]
    public async Task Insert_WithNullReport_ThrowsArgumentException(string? report)
    {
        // Arrange
        var deadDeliveryReport = new DeadDeliveryReport
        {
            FirstSeen = DateTime.UtcNow,
            LastAttempt = DateTime.UtcNow.AddMinutes(5),
            DeliveryReport = report!, 
            Channel = DeliveryReportChannel.AzureCommunicationServices, 
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
