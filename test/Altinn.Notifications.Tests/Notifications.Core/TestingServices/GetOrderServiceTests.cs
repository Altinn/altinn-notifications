using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class GetOrderServiceTests
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly string _creator = "ttd";
    private readonly string _sendersRef = "sendersRef";

    [Fact]
    public async Task GetOrderById_RepoReturnsNull_ServiceErrorReturned()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetOrderById(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((NotificationOrder?)null);

        var service = GetTestService(repoMock.Object);

        // Act
        var (actualOrder, actuallError) = await service.GetOrderById(_id, _creator);

        // Assert
        Assert.Null(actualOrder);
        Assert.NotNull(actuallError);
        Assert.Equal(404, actuallError.ErrorCode);
    }

    [Fact]
    public async Task GetOrderById_RepoThrowsException_ExceptionReturnedToCaller()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetOrderById(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("this is an unknown exception"));

        var service = GetTestService(repoMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => await service.GetOrderById(_id, _creator));
    }

    [Fact]
    public async Task GetOrderById_GetOrderByIdCalledInRepo_OrderReturned()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetOrderById(It.Is<Guid>(g => g == _id), It.Is<string>(s => s.Equals("ttd"))))
            .ReturnsAsync(new NotificationOrder() { Id = Guid.Parse(_id.ToString()) });

        var service = GetTestService(repoMock.Object);

        // Act
        var (actualOrder, actuallError) = await service.GetOrderById(_id, _creator);

        // Assert
        repoMock.Verify(r => r.GetOrderById(It.Is<Guid>(g => g == _id), It.Is<string>(s => s.Equals("ttd"))), Times.Once);
        Assert.NotNull(actualOrder);
        Assert.Null(actuallError);
        Assert.Equal(_id, actualOrder.Id);
    }

    [Fact]
    public async Task GetOrdersBySendersReference_RepoThrowsException_ExceptionReturnedToCaller()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetOrdersBySendersReference(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("this is an unknown exception"));

        var service = GetTestService(repoMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => await service.GetOrdersBySendersReference(_sendersRef, _creator));
    }

    [Fact]
    public async Task GetOrdersBySendersReference_GetOrdersBySendersReferenceCalledInRepo_ListOfOrdersReturned()
    {
        // Arrange
        var repoMock = new Mock<IOrderRepository>();
        repoMock
            .Setup(r => r.GetOrdersBySendersReference(It.Is<string>(s => s.Equals("sendersRef")), It.Is<string>(s => s.Equals("ttd"))))
            .ReturnsAsync(new List<NotificationOrder>() { new NotificationOrder(), new NotificationOrder(), new NotificationOrder() });

        var service = GetTestService(repoMock.Object);

        // Act
        var (actualOrders, actuallError) = await service.GetOrdersBySendersReference(_sendersRef, _creator);

        // Assert
        repoMock.Verify(r => r.GetOrdersBySendersReference(It.Is<string>(s => s.Equals("sendersRef")), It.Is<string>(s => s.Equals("ttd"))), Times.Once);
        Assert.NotNull(actualOrders);
        Assert.Equal(3, actualOrders.Count);
        Assert.Null(actuallError);
    }

    private static GetOrderService GetTestService(IOrderRepository? repo = null)
    {
        if (repo == null)
        {
            var _repo = new Mock<IOrderRepository>();
            repo = _repo.Object;
        }

        return new GetOrderService(repo);
    }
}