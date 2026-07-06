using System.Net;

using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Email.Integrations.Clients;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Azure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations
{
    public class EmailServiceClientTests
    {
        [Theory]
        [InlineData("Random error message not mentioning specific seconds", 60)]
        [InlineData("PerSubscriptionPerMinuteLimitExceeded - Please try again after 60 seconds.", 60)]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 3636 seconds.", 3636)]
        [InlineData("PerSubscriptionPerHourLimitExceeded - Please try again after 4000 seconds. Status: 429 (Too Many Requests) ErrorCode: TooManyRequests", 4000)]
        public void GetDelayFromString_WithVariousMessages_ReturnsExpectedDelay(string input, int expectedDelay)
        {
            // Arrange
            var communicationServicesSettings = new CommunicationServicesSettings
            {
                ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey"
            };
            
            var emailServiceAdminSettings = new EmailServiceAdminSettings
            {
                IntermittentErrorDelay = 60
            };

            var loggerMock = new Mock<ILogger<EmailServiceClient>>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var wolverineOptions = Options.Create(new WolverineSettings());
            var client = new EmailServiceClient(communicationServicesSettings, emailServiceAdminSettings, httpClientFactoryMock.Object, wolverineOptions, loggerMock.Object);

            // Act
            var result = client.GetDelayFromString(input);

            // Assert
            Assert.Equal(expectedDelay, result);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(300)]
        public void GetUnknownErrorDelay_WithConfiguredDelay_ReturnsConfiguredValue(int configuredDelay)
        {
            // Arrange
            var communicationServicesSettings = new CommunicationServicesSettings
            {
                ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey"
            };
            var emailServiceAdminSettings = new EmailServiceAdminSettings
            {
                IntermittentErrorDelay = configuredDelay
            };
            var loggerMock = new Mock<ILogger<EmailServiceClient>>();
            var wolverineOptions = Options.Create(new WolverineSettings());
            var httpClientFactoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
            var client = new EmailServiceClient(communicationServicesSettings, emailServiceAdminSettings, httpClientFactoryMock.Object, wolverineOptions, loggerMock.Object);

            // Act
            var result = client.GetUnknownErrorDelay();

            // Assert
            Assert.Equal(configuredDelay, result);
        }

        [Theory]
        [InlineData(500, EmailSendResult.Failed_TransientError)] // Internal Server Error
        [InlineData(502, EmailSendResult.Failed_TransientError)] // Bad Gateway
        [InlineData(503, EmailSendResult.Failed_TransientError)] // Service Unavailable
        [InlineData(504, EmailSendResult.Failed_TransientError)] // Gateway Timeout
        [InlineData(599, EmailSendResult.Failed_TransientError)] // Edge case - highest 5xx
        [InlineData(0, EmailSendResult.Failed_TransientError)] // Status 0 - network/no response
        [InlineData(413, EmailSendResult.Failed_PayloadTooLarge)] // Payload Too Large - attachment size exceeds ACS limit
        [InlineData(400, EmailSendResult.Failed)] // Bad Request - not transient
        [InlineData(404, EmailSendResult.Failed)] // Not Found - not transient
        [InlineData(429, EmailSendResult.Failed)] // Too Many Requests - handled separately by error code, not status
        public void GetEmailSendResult_WithVariousStatusCodes_ReturnsExpectedResult(int statusCode, EmailSendResult expectedResult)
        {
            // Arrange
            var exception = new RequestFailedException(statusCode, "Test error message");

            // Act
            var result = EmailServiceClient.GetEmailSendResult(exception);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(403)]
        [InlineData(404)]
        public async Task SendComposedEmail_BlobReturns4xx_ThrowsInvalidSasUrlException(int statusCode)
        {
            EmailServiceClient client = BuildClientWithHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)));

            await Assert.ThrowsAsync<InvalidSasUrlException>(() =>
                client.SendComposedEmail(MakeSingleAttachmentEmail(), TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(500)]
        [InlineData(503)]
        public async Task SendComposedEmail_BlobReturns5xx_ThrowsInvalidOperationException(int statusCode)
        {
            EmailServiceClient client = BuildClientWithHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.SendComposedEmail(MakeSingleAttachmentEmail(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendComposedEmail_NetworkError_ThrowsInvalidOperationException()
        {
            EmailServiceClient client = BuildClientWithHandler((_, _) =>
                throw new HttpRequestException("simulated network error"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.SendComposedEmail(MakeSingleAttachmentEmail(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendComposedEmail_FirstAttachmentFails_CancelsRemainingDownloads()
        {
            // Concurrency = 2 so both downloads start in parallel.
            // The second handler blocks indefinitely until its cancellation token fires.
            // If the first failure correctly cancels the linked CTS the second task unblocks
            // and the whole call completes. Without cancellation it hangs and the WaitAsync timeout fires.
            const string url1 = "https://blob.test/a.pdf?sas=1";
            const string url2 = "https://blob.test/b.pdf?sas=2";

            EmailServiceClient client = BuildClientWithHandler(
                async (request, ct) =>
                {
                    if (request.RequestUri!.AbsoluteUri == url1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.Forbidden);
                    }

                    await Task.Delay(Timeout.Infinite, ct);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                },
                blobConcurrency: 2);

            ComposedEmail email = new(
                Guid.NewGuid(),
                "s",
                "b",
                "from@test.no",
                "to@test.no",
                EmailContentType.Plain,
                [
                    new SasFileAttachment { Filename = "a.pdf", MimeType = "application/pdf", SasUrl = url1 },
                    new SasFileAttachment { Filename = "b.pdf", MimeType = "application/pdf", SasUrl = url2 }
                ]);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                client.SendComposedEmail(email, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        private static EmailServiceClient BuildClientWithHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
            int blobConcurrency = 5)
        {
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(new FakeBlobHandler(respond)));

            return new EmailServiceClient(
                new CommunicationServicesSettings
                {
                    ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=testkey"
                },
                new EmailServiceAdminSettings { IntermittentErrorDelay = 60 },
                httpClientFactoryMock.Object,
                Options.Create(new WolverineSettings { BlobDownloadConcurrency = blobConcurrency }),
                new Mock<ILogger<EmailServiceClient>>().Object);
        }

        private static ComposedEmail MakeSingleAttachmentEmail() =>
            new(
                Guid.NewGuid(),
                "subject",
                "body",
                "from@test.no",
                "to@test.no",
                EmailContentType.Plain,
                [new SasFileAttachment { Filename = "file.pdf", MimeType = "application/pdf", SasUrl = "https://blob.test/file.pdf?sas=token" }]);

        private sealed class FakeBlobHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                respond(request, cancellationToken);
        }
    }
}
