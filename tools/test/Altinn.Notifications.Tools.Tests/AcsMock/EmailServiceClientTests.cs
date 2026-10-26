using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Configuration;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

using SendingEmail = Altinn.Notifications.Email.Core.Sending.Email;

namespace Altinn.Notifications.Tools.Tests.AcsMock;

/// <summary>
/// Runs the mock on a real port and sends through the email service's own <see cref="EmailServiceClient"/>,
/// which builds its <c>EmailClient</c> from the connection string, so this also shows the SDK accepts an http endpoint.
/// </summary>
public class EmailServiceClientTests(EmailServiceClientTests.KestrelAcsMockFactory factory) : IClassFixture<EmailServiceClientTests.KestrelAcsMockFactory>
{
    [Theory]
    [InlineData("ola.nordmann@example.com", EmailSendResult.Succeeded)]
    [InlineData("bounce@example.com", EmailSendResult.Succeeded)]
    [InlineData("fail@example.com", EmailSendResult.Failed)]
    [InlineData("suppressed@example.com", EmailSendResult.Failed_SupressedRecipient)]
    public async Task SendThenPoll_MapsScenarioToSendResult(string recipient, EmailSendResult expected)
    {
        var client = CreateClient(AcsMockFactory.AccessKey);

        var sent = await client.SendEmail(NewEmail(recipient), TestContext.Current.CancellationToken);
        string operationId = sent.Match(id => id, error => throw new InvalidOperationException(error.SendResult.ToString()));

        Assert.Equal(expected, await client.GetOperationUpdate(operationId));
    }

    [Theory]
    [InlineData("invalid@example.com", EmailSendResult.Failed_InvalidEmailFormat)]
    [InlineData("throttle@example.com", EmailSendResult.Failed_TransientError)]
    public async Task Send_RejectedScenario_MapsToSendResult(string recipient, EmailSendResult expected)
    {
        var client = CreateClient(AcsMockFactory.AccessKey);

        var sent = await client.SendEmail(NewEmail(recipient), TestContext.Current.CancellationToken);

        Assert.Equal(expected, sent.Match(_ => EmailSendResult.Succeeded, error => error.SendResult));
    }

    [Fact]
    public async Task Send_WithWrongAccessKey_Fails()
    {
        var client = CreateClient(Convert.ToBase64String("wrong-key"u8.ToArray()));

        var sent = await client.SendEmail(NewEmail("ola.nordmann@example.com"), TestContext.Current.CancellationToken);

        Assert.Equal(EmailSendResult.Failed, sent.Match(_ => EmailSendResult.Succeeded, error => error.SendResult));
    }

    private EmailServiceClient CreateClient(string accessKey)
    {
        return new EmailServiceClient(
            new CommunicationServicesSettings { ConnectionString = $"endpoint={factory.Url};accesskey={accessKey}" },
            new EmailServiceAdminSettings { IntermittentErrorDelay = 60, BlobDownloadConcurrency = 1, BlobDownloadTimeoutInSeconds = 1 },
            Mock.Of<IHttpClientFactory>(),
            NullLogger<EmailServiceClient>.Instance);
    }

    private static SendingEmail NewEmail(string recipient)
    {
        return new SendingEmail(Guid.NewGuid(), "Subject", "Body", "noreply@altinn.example", recipient, EmailContentType.Plain);
    }

    public class KestrelAcsMockFactory : AcsMockFactory
    {
        public KestrelAcsMockFactory()
        {
            UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            StartServer();
        }

        public string Url => Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First() + "/";
    }
}
