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
    public async Task Add_WithNullReport_ThrowsArgumentException(string? report)
    {
        // Arrange
        var channel = DeliveryReportChannel.AzureCommunicationServices;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.Add(report!, channel, CancellationToken.None));

        Assert.Equal("Report cannot be null or empty (Parameter 'report')", exception.Message);
        Assert.Equal("report", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.Add(It.IsAny<Altinn.Notifications.Core.Models.DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Add_WithValidReport_CallsRepositorySuccessfully()
    {
        // Arrange
        string validReport = "{}";
        var channel = DeliveryReportChannel.AzureCommunicationServices;
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(x => x.Add(It.IsAny<DeadDeliveryReport>(), cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Add(validReport, channel, cancellationToken);

        // Assert
        _repositoryMock.Verify(
            x => x.Add(
                It.Is<DeadDeliveryReport>(report => report.DeliveryReport == validReport && report.Channel == channel && report.AttemptCount == 0 && !report.Resolved),
                cancellationToken),
            Times.Once);
    }
}
