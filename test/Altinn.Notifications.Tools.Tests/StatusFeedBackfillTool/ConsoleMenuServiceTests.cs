using System;
using System.Threading.Tasks;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Services;
using Altinn.Notifications.Tools.StatusFeedBackfillTool.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.StatusFeedBackfillTool
{
    public class ConsoleMenuServiceTests
    {
        [Fact]
        public async Task RunMenuAsync_ExitOption_PrintsExitAndReturnsZero()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            // Simulate user input '5' for exit
            var input = new System.IO.StringReader("5\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            Assert.Contains("Exiting", output.ToString());
        }

        [Fact]
        public async Task RunMenuAsync_DiscoverOption_CallsOrderDiscoveryService()
        {
            // Arrange
            var discoveryServiceMock = new Mock<IOrderDiscoveryService>(MockBehavior.Strict);
            discoveryServiceMock.Setup(s => s.Run()).Returns(Task.CompletedTask).Verifiable();

            var services = new ServiceCollection();
            services.AddSingleton(discoveryServiceMock.Object);
            services.AddSingleton(new Mock<IStatusFeedBackfillService>(MockBehavior.Loose).Object);
            services.AddSingleton(new Mock<ITestDataService>(MockBehavior.Loose).Object);
            var serviceProvider = services.BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            var input = new System.IO.StringReader("1\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            discoveryServiceMock.Verify(s => s.Run(), Times.Once);
        }

        [Fact]
        public async Task RunMenuAsync_BackfillOption_CallsStatusFeedBackfillService()
        {
            // Arrange
            var backfillServiceMock = new Mock<IStatusFeedBackfillService>(MockBehavior.Strict);
            backfillServiceMock.Setup(s => s.Run()).Returns(Task.CompletedTask).Verifiable();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<IOrderDiscoveryService>(MockBehavior.Loose).Object);
            services.AddSingleton(backfillServiceMock.Object);
            services.AddSingleton(new Mock<ITestDataService>(MockBehavior.Loose).Object);
            var serviceProvider = services.BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            var input = new System.IO.StringReader("2\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            backfillServiceMock.Verify(s => s.Run(), Times.Once);
        }

        [Fact]
        public async Task RunMenuAsync_GenerateTestDataOption_CallsGenerateTestData()
        {
            // Arrange
            var testDataServiceMock = new Mock<ITestDataService>(MockBehavior.Strict);
            testDataServiceMock.Setup(s => s.GenerateTestData()).Returns(Task.CompletedTask).Verifiable();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<IOrderDiscoveryService>(MockBehavior.Loose).Object);
            services.AddSingleton(new Mock<IStatusFeedBackfillService>(MockBehavior.Loose).Object);
            services.AddSingleton(testDataServiceMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            var input = new System.IO.StringReader("3\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            testDataServiceMock.Verify(s => s.GenerateTestData(), Times.Once);
        }

        [Fact]
        public async Task RunMenuAsync_CleanupTestDataOption_Yes_CallsCleanupTestData()
        {
            // Arrange
            var testDataServiceMock = new Mock<ITestDataService>(MockBehavior.Strict);
            testDataServiceMock.Setup(s => s.CleanupTestData()).Returns(Task.CompletedTask).Verifiable();

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<IOrderDiscoveryService>(MockBehavior.Loose).Object);
            services.AddSingleton(new Mock<IStatusFeedBackfillService>(MockBehavior.Loose).Object);
            services.AddSingleton(testDataServiceMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            var input = new System.IO.StringReader("4\ny\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            testDataServiceMock.Verify(s => s.CleanupTestData(), Times.Once);
            Assert.DoesNotContain("Cleanup cancelled", output.ToString());
        }

        [Fact]
        public async Task RunMenuAsync_CleanupTestDataOption_No_PrintsCancelled()
        {
            // Arrange
            var testDataServiceMock = new Mock<ITestDataService>(MockBehavior.Strict);

            var services = new ServiceCollection();
            services.AddSingleton(new Mock<IOrderDiscoveryService>(MockBehavior.Loose).Object);
            services.AddSingleton(new Mock<IStatusFeedBackfillService>(MockBehavior.Loose).Object);
            services.AddSingleton(testDataServiceMock.Object);
            var serviceProvider = services.BuildServiceProvider();
            var menuService = new ConsoleMenuService(serviceProvider);

            var input = new System.IO.StringReader("4\nn\n");
            Console.SetIn(input);
            var output = new System.IO.StringWriter();
            Console.SetOut(output);

            // Act
            int result = await menuService.RunMenuAsync();

            // Assert
            Assert.Equal(0, result);
            testDataServiceMock.Verify(s => s.CleanupTestData(), Times.Never);
            Assert.Contains("Cleanup cancelled", output.ToString());
        }
    }
}
