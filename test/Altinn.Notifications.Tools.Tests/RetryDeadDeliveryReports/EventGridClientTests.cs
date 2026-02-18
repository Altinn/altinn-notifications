using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Tools.RetryDeadDeliveryReports.EventGrid;

using Microsoft.Extensions.Options;
using Xunit;

namespace Altinn.Notifications.Tools.Tests.RetryDeadDeliveryReports;

public class EventGridClientTests
{
    [Fact]
    public async Task PostEventsAsync_ReturnsSuccess_WhenHandlerReturnsOk()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok")
        };
        using var handler = new FakeHandler(response);
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
        using var handler = new FakeHandler(response);
        var http = new HttpClient(handler);
        var settings = Options.Create(new EventGridSettings { BaseUrl = "https://example.com/events", AccessKey = "key" });

        var client = new EventGridClient(http, settings);

        var (success, body) = await client.PostEventsAsync([new { id = "1" }]);

        Assert.False(success);
        Assert.Equal("bad", body);
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
