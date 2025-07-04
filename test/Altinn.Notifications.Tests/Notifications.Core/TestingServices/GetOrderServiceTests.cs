﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

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
        Result<NotificationOrder, ServiceError> result = await service.GetOrderById(_id, _creator);

        // Assert
        Assert.True(result.IsError);
        await result.Match<Task>(
           async actualOrder => await Task.CompletedTask,
           async actuallError =>
           {
               await Task.CompletedTask;
               Assert.NotNull(actuallError);
               Assert.Equal(404, actuallError.ErrorCode);
           });
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
        Result<NotificationOrder, ServiceError> result = await service.GetOrderById(_id, _creator);

        // Assert
        Assert.True(result.IsSuccess);
        await result.Match<Task>(
           async actualOrder =>
           {
               await Task.CompletedTask;
               repoMock.Verify(r => r.GetOrderById(It.Is<Guid>(g => g == _id), It.Is<string>(s => s.Equals("ttd"))), Times.Once);
               Assert.NotNull(actualOrder);
               Assert.Equal(_id, actualOrder.Id);
           },
           async actuallError => await Task.CompletedTask);
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
        Result<List<NotificationOrder>, ServiceError> result = await service.GetOrdersBySendersReference(_sendersRef, _creator);

        // Assert
        Assert.True(result.IsSuccess);
        await result.Match<Task>(
           async actualOrders =>
           {
               await Task.CompletedTask;

               repoMock.Verify(r => r.GetOrdersBySendersReference(It.Is<string>(s => s.Equals("sendersRef")), It.Is<string>(s => s.Equals("ttd"))), Times.Once);
               Assert.NotNull(actualOrders);
               Assert.Equal(3, actualOrders.Count);
           },
           async actuallError => await Task.CompletedTask);
    }

    [Theory]
    [InlineData(OrderProcessingStatus.Cancelled, "Order processing was stopped due to order being cancelled.")]
    [InlineData(OrderProcessingStatus.Processing, "Order processing is ongoing. Notifications are being generated.")]
    [InlineData(OrderProcessingStatus.Processed, "Order processing is done. Notifications have been successfully generated.")]
    [InlineData(OrderProcessingStatus.Completed, "Order processing is completed. All notifications have a final status.")]
    [InlineData(OrderProcessingStatus.SendConditionNotMet, "Order processing was stopped due to send condition not being met.")]
    [InlineData(OrderProcessingStatus.Registered, "Order has been registered and is awaiting requested send time before processing.")]
    public void GetStatusDescription_ExpectedDescription(OrderProcessingStatus status, string expected)
    {
        string actual = GetOrderService.GetStatusDescription(status);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetStatusDescription_AllResultTypesHaveDescriptions()
    {
        foreach (OrderProcessingStatus statusType in Enum.GetValues(typeof(OrderProcessingStatus)))
        {
            string statusDescription = GetOrderService.GetStatusDescription(statusType);
            Assert.NotEmpty(statusDescription);
        }
    }

    private static GetOrderService GetTestService(IOrderRepository? repo = null)
    {
        if (repo == null)
        {
            var repoMock = new Mock<IOrderRepository>();
            repo = repoMock.Object;
        }

        return new GetOrderService(repo);
    }
}
