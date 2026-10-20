using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services;
using Altinn.Register.Contracts;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class KeywordsServiceTests
{
    private const string _personNumber = "25871999336";
    private const string _organizationNumber = "123456785";

    private readonly KeywordsService _keywordsService;
    private readonly Mock<IRegisterClient> _registerClientMock;

    public KeywordsServiceTests()
    {
        _registerClientMock = new Mock<IRegisterClient>();
        _keywordsService = new KeywordsService(_registerClientMock.Object);
    }

    [Fact]
    public void ContainsRecipientNamePlaceholder_ShouldReturnTrue_WhenPlaceholderExists()
    {
        // Arrange
        var value = "Hello $recipientName$";

        // Act
        var result = _keywordsService.ContainsRecipientNamePlaceholder(value);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsRecipientNamePlaceholder_ShouldReturnFalse_WhenPlaceholderDoesNotExist()
    {
        // Arrange
        var value = "Hello World";

        // Act
        var result = _keywordsService.ContainsRecipientNamePlaceholder(value);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsRecipientNumberPlaceholder_ShouldReturnTrue_WhenPlaceholderExists()
    {
        // Arrange
        var value = "Your number is $recipientNumber$";

        // Act
        var result = _keywordsService.ContainsRecipientNumberPlaceholder(value);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsRecipientNumberPlaceholder_ShouldReturnFalse_WhenPlaceholderDoesNotExist()
    {
        // Arrange
        var value = "Your number is 12345";

        // Act
        var result = _keywordsService.ContainsRecipientNumberPlaceholder(value);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_EmailRecipients_ShouldReplacePlaceholdersForPersons()
    {
        // Arrange
        var emailRecipients = new List<EmailRecipient>
        {
            new()
            {
                NationalIdentityNumber = _personNumber,
                CustomizedBody = "Hello $recipientName$",
                CustomizedSubject = "Subject $recipientNumber$",
            }
        };

        SetupParties(CreateParty("person", "personIdentifier", _personNumber, "Person name"));

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Person name", recipient.CustomizedBody);
        Assert.Equal("Subject ", recipient.CustomizedSubject);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_EmailRecipients_ShouldReplacePlaceholdersForOrganizations()
    {
        // Arrange
        var emailRecipients = new List<EmailRecipient>
        {
            new()
            {
                OrganizationNumber = _organizationNumber,
                CustomizedBody = "Hello $recipientName$",
                CustomizedSubject = "Subject $recipientNumber$",
            }
        };

        SetupParties(CreateParty("organization", "organizationIdentifier", _organizationNumber, "Organization name"));

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal($"Subject {_organizationNumber}", recipient.CustomizedSubject);
        Assert.Equal("Hello Organization name", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipient_ShouldReplacePlaceholdersForPersons()
    {
        // Arrange
        var smsRecipients = new List<SmsRecipient>
        {
            new()
            {
                NationalIdentityNumber = _personNumber,
                CustomizedBody = "Hello $recipientName$ your national identity number is $recipientNumber$"
            }
        };

        SetupParties(CreateParty("person", "personIdentifier", _personNumber, "Person name"));

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Person name your national identity number is ", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipients_ShouldReplacePlaceholdersForOrganizations()
    {
        // Arrange
        var smsRecipients = new List<SmsRecipient>
        {
            new()
            {
                OrganizationNumber = _organizationNumber,
                CustomizedBody = "Hello $recipientName$ your organization number is $recipientNumber$"
            }
        };

        SetupParties(CreateParty("organization", "organizationIdentifier", _organizationNumber, "Organization name"));

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal($"Hello Organization name your organization number is {_organizationNumber}", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipients_ShouldQueryDistinctIdentifiersOnce()
    {
        // Arrange
        var smsRecipients = Enumerable.Range(0, 3)
            .Select(_ => new SmsRecipient { OrganizationNumber = _organizationNumber, CustomizedBody = "Hello $recipientName$" })
            .ToList();

        var batchSizes = new List<int>();
        _registerClientMock.Setup(client => client.GetParties(It.IsAny<IReadOnlyCollection<PartyUrn>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<PartyUrn>, CancellationToken>((urns, _) => batchSizes.Add(urns.Count))
            .ReturnsAsync([]);

        // Act
        await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        Assert.Equal([1], batchSizes);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipients_ShouldSkipInvalidIdentifiers()
    {
        // Arrange
        var smsRecipients = new List<SmsRecipient>
        {
            new() { OrganizationNumber = "123", NationalIdentityNumber = "456", CustomizedBody = "Hello $recipientName$" }
        };

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        Assert.Equal("Hello $recipientName$", result.First().CustomizedBody);
        _registerClientMock.Verify(
            client => client.GetParties(It.IsAny<IReadOnlyCollection<PartyUrn>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_EmailRecipients_ShouldHandleEmptyLists()
    {
        // Arrange
        var emailRecipients = new List<EmailRecipient>();

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipients_ShouldHandleEmptyLists()
    {
        // Arrange
        var smsRecipients = new List<SmsRecipient>();

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_EmailRecipients_ShouldHandleNullValues()
    {
        // Arrange
        var emailRecipients = new List<EmailRecipient>
        {
            new()
            {
                OrganizationNumber = null,
                NationalIdentityNumber = null,
                CustomizedBody = "Hello $recipientName$",
                CustomizedSubject = "Subject $recipientNumber$",
            }
        };

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello $recipientName$", recipient.CustomizedBody);
        Assert.Equal("Subject $recipientNumber$", recipient.CustomizedSubject);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SMSRecipients_ShouldHandleNullValues()
    {
        // Arrange
        var smsRecipients = new List<SmsRecipient>
        {
            new()
            {
                OrganizationNumber = null,
                NationalIdentityNumber = null,
                CustomizedBody = "Hello $recipientName$ your number is $recipientNumber$"
            }
        };

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello $recipientName$ your number is $recipientNumber$", recipient.CustomizedBody);
    }

    private void SetupParties(params Party[] parties)
    {
        _registerClientMock.Setup(client => client.GetParties(It.IsAny<IReadOnlyCollection<PartyUrn>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. parties]);
    }

    private static Party CreateParty(string partyType, string identifierProperty, string identifier, string displayName)
    {
        var json = $$"""
            {
                "partyType": "{{partyType}}",
                "{{identifierProperty}}": "{{identifier}}",
                "partyUuid": "{{Guid.NewGuid()}}",
                "versionId": 1,
                "displayName": "{{displayName}}"
            }
            """;

        return JsonSerializer.Deserialize<Party>(json)!;
    }
}
