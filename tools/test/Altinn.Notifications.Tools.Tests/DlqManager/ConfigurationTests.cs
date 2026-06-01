using System;
using System.IO;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.DlqManager.Configuration;
using Altinn.Notifications.Tools.DlqManager.Services;
using Altinn.Notifications.Tools.DlqManager.Services.Queues;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tools.Tests.DlqManager;

public class ConfigurationTests
{
    [Fact]
    public void AsbSettings_DefaultValues_AreCorrect()
    {
        var settings = new AsbSettings();

        Assert.Equal(string.Empty, settings.ConnectionString);
        Assert.Equal("altinn.notifications.sms.send", settings.SmsSendQueueName);
    }

    [Fact]
    public void AsbSettings_CanSetProperties()
    {
        var settings = new AsbSettings
        {
            ConnectionString = "Endpoint=sb://test;",
            SmsSendQueueName = "custom.queue"
        };

        Assert.Equal("Endpoint=sb://test;", settings.ConnectionString);
        Assert.Equal("custom.queue", settings.SmsSendQueueName);
    }

    [Fact]
    public void PostgreSqlSettings_DefaultValues_AreCorrect()
    {
        var settings = new PostgreSqlSettings();

        Assert.Equal(string.Empty, settings.ConnectionString);
    }

    [Fact]
    public void PostgreSqlSettings_CanSetConnectionString()
    {
        var settings = new PostgreSqlSettings
        {
            ConnectionString = "Host=localhost;Database=notificationsdb;"
        };

        Assert.Equal("Host=localhost;Database=notificationsdb;", settings.ConnectionString);
    }

    [Fact]
    public void SmsSendQueueSettings_DefaultValues_AreCorrect()
    {
        var settings = new SmsSendQueueSettings();

        Assert.Equal("sms-send-dlq-sending-expired.json", settings.SendingExpiredListFilePath);
        Assert.Equal("sms-send-dlq-sending-pending.json", settings.SendingPendingListFilePath);
        Assert.Equal("sms-send-dlq-other.json", settings.OtherStatusListFilePath);
    }

    [Fact]
    public void SmsSendQueueSettings_CanSetProperties()
    {
        var settings = new SmsSendQueueSettings
        {
            SendingExpiredListFilePath = "/tmp/expired.json",
            SendingPendingListFilePath = "/tmp/pending.json",
            OtherStatusListFilePath = "/tmp/other.json"
        };

        Assert.Equal("/tmp/expired.json", settings.SendingExpiredListFilePath);
        Assert.Equal("/tmp/pending.json", settings.SendingPendingListFilePath);
        Assert.Equal("/tmp/other.json", settings.OtherStatusListFilePath);
    }
}

public class ConsoleMenuServiceTests
{
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
    public async Task RunMenuAsync_InvalidInput_PrintsErrorAndContinues()
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
