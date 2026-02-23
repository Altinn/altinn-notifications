using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.IntegrationTests.TestUtilities;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Tests.Notifications.Utils;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications;

/// <summary>
/// Integration tests verifying that the correct <see cref="OrderPhase"/> enum value is used
/// during order creation (<see cref="OrderPhase.NewOrder"/>) to ensure that the expensive
/// authorization lookup for user-registered contact points is skipped at order acceptance time.
/// The authorization is deferred to <see cref="OrderPhase.Processing"/> when the order is
/// picked up by the consumer for delivery.
/// </summary>
public class OrderPhaseIntegrationTests(SpyContactPointServiceFactory factory) : IClassFixture<SpyContactPointServiceFactory>, IAsyncLifetime
{
    private const string _basePath = "/notifications/api/v1/future/orders";
    private readonly SpyContactPointServiceFactory _factory = factory;
    private HttpClient _client = null!;

    [Fact]
    public async Task CreateEmailOrder_WithOrganizationRecipient_UsesNewOrderPhase()
    {
        // Arrange
        var callCountBefore = _factory.SpyService?.RecordedCalls.Count ?? 0;

        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5201C0F33A8C",
                    ChannelSchema = NotificationChannelExt.Email,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Test body",
                        Subject = "Test subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    }
                }
            }
        };

        // Act
        await SendPostRequest(request);

        // Assert
        Assert.NotNull(_factory.SpyService);
        Assert.True(_factory.SpyService.RecordedCalls.Count > callCountBefore);

        var emailCall = _factory.SpyService.RecordedCalls
            .FirstOrDefault(c => c.Method == "AddEmailContactPoints" && c.Phase == OrderPhase.NewOrder);

        Assert.NotEqual(default, emailCall);
    }

    [Fact]
    public async Task CreateSmsOrder_WithOrganizationRecipient_UsesNewOrderPhase()
    {
        // Arrange
        var callCountBefore = _factory.SpyService?.RecordedCalls.Count ?? 0;

        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5201C0F33A8C",
                    ChannelSchema = NotificationChannelExt.Sms,
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "Test SMS message",
                        Sender = "Altinn"
                    }
                }
            }
        };

        // Act
        await SendPostRequest(request);

        // Assert
        Assert.NotNull(_factory.SpyService);
        Assert.True(_factory.SpyService.RecordedCalls.Count > callCountBefore);

        var smsCall = _factory.SpyService.RecordedCalls
            .FirstOrDefault(c => c.Method == "AddSmsContactPoints" && c.Phase == OrderPhase.NewOrder);

        Assert.NotEqual(default, smsCall);
    }

    [Fact]
    public async Task CreateEmailAndSmsOrder_WithOrganizationRecipient_UsesNewOrderPhase()
    {
        // Arrange
        var callCountBefore = _factory.SpyService?.RecordedCalls.Count ?? 0;

        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5201C0F33A8C",
                    ChannelSchema = NotificationChannelExt.EmailAndSms,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "Altinn"
                    }
                }
            }
        };

        // Act
        await SendPostRequest(request);

        // Assert
        Assert.NotNull(_factory.SpyService);
        Assert.True(_factory.SpyService.RecordedCalls.Count > callCountBefore);

        var emailAndSmsCall = _factory.SpyService.RecordedCalls
            .FirstOrDefault(c => c.Method == "AddEmailAndSmsContactPointsAsync" && c.Phase == OrderPhase.NewOrder);

        Assert.NotEqual(default, emailAndSmsCall);
    }

    [Fact]
    public async Task CreatePreferredChannelOrder_WithOrganizationRecipient_UsesNewOrderPhase()
    {
        // Arrange
        var callCountBefore = _factory.SpyService?.RecordedCalls.Count ?? 0;

        var request = new NotificationOrderChainRequestExt
        {
            IdempotencyId = Guid.NewGuid().ToString(),
            RequestedSendTime = DateTime.UtcNow.AddHours(2),
            Recipient = new NotificationRecipientExt
            {
                RecipientOrganization = new RecipientOrganizationExt
                {
                    OrgNumber = "987654321",
                    ResourceId = "urn:altinn:resource:5201C0F33A8C",
                    ChannelSchema = NotificationChannelExt.EmailPreferred,
                    EmailSettings = new EmailSendingOptionsExt
                    {
                        Body = "Email body",
                        Subject = "Email subject",
                        SenderEmailAddress = "sender@example.com",
                        ContentType = EmailContentTypeExt.Plain
                    },
                    SmsSettings = new SmsSendingOptionsExt
                    {
                        Body = "SMS body",
                        Sender = "Altinn"
                    }
                }
            }
        };

        // Act
        await SendPostRequest(request);

        // Assert
        Assert.NotNull(_factory.SpyService);
        Assert.True(_factory.SpyService.RecordedCalls.Count > callCountBefore);

        var preferredCall = _factory.SpyService.RecordedCalls
            .FirstOrDefault(c => c.Method == "AddPreferredContactPoints" && c.Phase == OrderPhase.NewOrder);

        Assert.NotEqual(default, preferredCall);
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<HttpResponseMessage> SendPostRequest(NotificationOrderChainRequestExt request)
    {
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        return await _client.PostAsync(_basePath, content);
    }
}
