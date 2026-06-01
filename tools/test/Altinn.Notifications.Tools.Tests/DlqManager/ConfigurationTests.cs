using Altinn.Notifications.Tools.DlqManager.Configuration;

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
