using Microsoft.Extensions.Options;
using System.Net;
using Tools;
using Tools.EventGrid;

namespace ToolsTests;

class FakeHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public FakeHandler(HttpResponseMessage response) => _response = response;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_response);
    }
}

public class EventGridClientTests
{
    [Fact]
    public async Task PostEventsAsync_ReturnsSuccess_WhenHandlerReturnsOk()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        };
        var handler = new FakeHandler(response);
        var http = new HttpClient(handler);
        var settings = Options.Create(new EventGridSettings { BaseUrl = "https://example.com/events", AccessKey = "key" });

        var client = new EventGridClient(http, settings);

        var (success, body) = await client.PostEventsAsync(new[] { new { id = "1" } });

        Assert.True(success);
        Assert.Equal("ok", body);
    }

    [Fact]
    public async Task PostEventsAsync_ReturnsFalse_WhenHandlerReturnsError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad")
        };
        var handler = new FakeHandler(response);
        var http = new HttpClient(handler);
        var settings = Options.Create(new EventGridSettings { BaseUrl = "https://example.com/events", AccessKey = "key" });

        var client = new EventGridClient(http, settings);

        var (success, body) = await client.PostEventsAsync([new { id = "1" }]);

        Assert.False(success);
        Assert.Equal("bad", body);
    }
}
