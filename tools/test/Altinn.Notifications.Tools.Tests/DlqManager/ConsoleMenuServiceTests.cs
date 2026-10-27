using System;
using System.IO;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Services;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.DlqManager;

public class ConsoleMenuServiceTests
{
    [Fact]
    public async Task RunMenuAsync_WhenInputStreamEnds_ExitsGracefully()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader(string.Empty));
            Console.SetOut(TextWriter.Null);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_ExitOption_ReturnsZero()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader("0\n"));
            Console.SetOut(TextWriter.Null);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_NonNumericInput_PrintsErrorAndContinues()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetIn(new StringReader("abc\n0\n"));
            Console.SetOut(output);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
            Assert.Contains("Invalid choice", output.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_NegativeIntegerInput_PrintsErrorAndContinues()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetIn(new StringReader("-1\n0\n"));
            Console.SetOut(output);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
            Assert.Contains("Invalid choice", output.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_OutOfRangeIntegerInput_PrintsErrorAndContinues()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetIn(new StringReader("99\n0\n"));
            Console.SetOut(output);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
            Assert.Contains("Invalid choice", output.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_NotImplementedQueue_PrintsMessageAndContinues()
    {
        var service = new ConsoleMenuService(new ServiceCollection().BuildServiceProvider());
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Console.SetIn(new StringReader("2\n0\n"));
            Console.SetOut(output);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
            Assert.Contains("not yet implemented", output.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunMenuAsync_SmsSendOption_CallsSmsSendQueueService()
    {
        var mockSmsSendService = new Mock<ISmsSendQueueService>();
        mockSmsSendService.Setup(s => s.RunMenuAsync()).Returns(Task.CompletedTask).Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(mockSmsSendService.Object);
        var service = new ConsoleMenuService(services.BuildServiceProvider());

        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetIn(new StringReader("1\n0\n"));
            Console.SetOut(TextWriter.Null);

            int result = await service.RunMenuAsync();

            Assert.Equal(0, result);
            mockSmsSendService.Verify(s => s.RunMenuAsync(), Times.Once);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }
}
