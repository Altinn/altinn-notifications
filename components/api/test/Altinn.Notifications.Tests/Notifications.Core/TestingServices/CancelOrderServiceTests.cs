using System;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Services
{
    public class CancelOrderServiceTests
    {
        private readonly Mock<IOrderRepository> _repositoryMock;
        private readonly CancelOrderService _cancelOrderService;

        public CancelOrderServiceTests()
        {
            _repositoryMock = new Mock<IOrderRepository>();
            _cancelOrderService = new CancelOrderService(_repositoryMock.Object);
        }

        [Fact]
        public async Task CancelOrder_SuccessfullyCancelled_ReturnsOrderWithStatus()
        {
            // Arrange      
            Guid orderId = Guid.NewGuid();

            _repositoryMock.Setup(r => r.CancelOrder(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(new NotificationOrderWithStatus()
                {
                    Id = orderId,
                    ProcessingStatus = new()
                    {
                        Status = OrderProcessingStatus.Cancelled
                    }
                });

            // Act
            var result = await _cancelOrderService.CancelOrder(orderId, "ttd");

            // Assert
            result.Match(
                success =>
                {
                    Assert.Equal(OrderProcessingStatus.Cancelled, success.ProcessingStatus.Status);
                    Assert.False(string.IsNullOrEmpty(success.ProcessingStatus.StatusDescription));
                    return true;
                },
                error => throw new Exception("No error value should be returned if order successfully cancelled."));
        }

        [Fact]
        public async Task CancelOrder_OrderDoesNotExist_ReturnsCancellationError()
        {  
            // Arrange      
            Guid orderId = Guid.NewGuid();

            _repositoryMock.Setup(r => r.CancelOrder(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(CancellationError.OrderNotFound);

            // Act
            var result = await _cancelOrderService.CancelOrder(orderId, "ttd");

            // Assert
            result.Match(
                success => throw new Exception("No success value should be returned if order is not found."),
                error => 
                {
                    Assert.Equal(CancellationError.OrderNotFound, error);
                    return true;
                });
        }

        [Fact]
        public async Task CancelOrder_OrderNotCancelled_ReturnsCancellationError()
        {
            // Arrange      
            Guid orderId = Guid.NewGuid();

            _repositoryMock.Setup(r => r.CancelOrder(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(CancellationError.CancellationProhibited);

            // Act
            var result = await _cancelOrderService.CancelOrder(orderId, "ttd");

            // Assert
            result.Match(
                success => throw new Exception("No success value should be returned if order is not found."),
                error =>
                {
                    Assert.Equal(CancellationError.CancellationProhibited, error);
                    return true;
                });
        }
    }
}
