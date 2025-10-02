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
        var deadDeliveryReport = new DeadDeliveryReport
        {
            DeliveryReport = report!, 
            Channel = DeliveryReportChannel.AzureCommunicationServices, 
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.Insert(deadDeliveryReport, CancellationToken.None));

        Assert.Equal("report.DeliveryReport cannot be null or empty (Parameter 'DeliveryReport')", exception.Message);
        Assert.Equal("DeliveryReport", exception.ParamName);

        // Verify repository was never called
        _repositoryMock.Verify(
            x => x.Insert(It.IsAny<Altinn.Notifications.Core.Models.DeadDeliveryReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Add_WithValidReport_CallsRepositorySuccessfully()
    {
        // Arrange
        var deadDeliveryReport = new DeadDeliveryReport
        {
            DeliveryReport = "{}",
            Channel = DeliveryReportChannel.AzureCommunicationServices,
        };
        
        _repositoryMock
            .Setup(x => x.Insert(It.IsAny<DeadDeliveryReport>(), CancellationToken.None))
            .Returns(Task.FromResult(1L));

        // Act
        await _sut.Insert(deadDeliveryReport, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            x => x.Insert(
                It.Is<DeadDeliveryReport>(report => report.DeliveryReport == "{}" && report.Channel == DeliveryReportChannel.AzureCommunicationServices),
                CancellationToken.None),
            Times.Once);
    }
}
