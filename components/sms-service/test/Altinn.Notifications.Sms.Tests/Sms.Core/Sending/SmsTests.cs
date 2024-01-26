using System.Text.Json.Nodes;

namespace Altinn.Notifications.Sms.Tests.Sms.Core.Sending;

public class SmsTests
{
    private readonly string _serializedSms;
    private readonly Guid _id;

    public SmsTests()
    {
        _id = Guid.NewGuid();
        _serializedSms = new JsonObject()
        {
            { "notificationId", _id },
            { "recipient", "recipient" },
            { "sender", "sender" },
            { "message", "message" }
        }.ToJsonString();
    }

    [Fact]
    public void TryParse_ValidSms_True()
    {
        bool actualResult = Notifications.Sms.Core.Sending.Sms.TryParse(_serializedSms, out Notifications.Sms.Core.Sending.Sms actual);
        
        Assert.True(actualResult);
        Assert.Equal(_id, actual.NotificationId);
        Assert.Equal("message", actual.Message);
    }

    [Fact]
    public void TryParse_EmptyString_False()
    {
        bool actualResult = Notifications.Sms.Core.Sending.Sms.TryParse(string.Empty, out _);
        
        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidGuid_False()
    {
        bool actualResult = Notifications.Sms.Core.Sending.Sms.TryParse("{\"notificationId\":\"thisIsNotAGuid\"}", out _);

        Assert.False(actualResult);
    }

    [Fact]
    public void TryParse_InvalidJsonExceptionThrown_False()
    {
        bool actualResult = Notifications.Sms.Core.Sending.Sms.TryParse("{\"fakefield\"=nothing\"}", out _);

        Assert.False(actualResult);
    }
}
