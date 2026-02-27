using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class KeywordsServiceTests
{
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
                NationalIdentityNumber = "07837399275",
                CustomizedBody = "Hello $recipientName$",
                CustomizedSubject = "Subject $recipientNumber$",
            }
        };

        var personDetails = new List<PartyDetails>
        {
            new() { NationalIdentityNumber = "07837399275", Name = "Person name" }
        };

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync(personDetails);

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
                OrganizationNumber = "313997901",
                CustomizedBody = "Hello $recipientName$",
                CustomizedSubject = "Subject $recipientNumber$",
            }
        };

        var organizationDetails = new List<PartyDetails>
        {
            new() { OrganizationNumber = "313997901", Name = "Organization name" }
        };

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync(organizationDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Subject 313997901", recipient.CustomizedSubject);
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
                NationalIdentityNumber = "07837399275",
                CustomizedBody = "Hello $recipientName$ your national identity number is $recipientNumber$"
            }
        };

        var personDetails = new List<PartyDetails>
        {
            new() { NationalIdentityNumber = "07837399275", Name = "Person name" }
        };

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync(personDetails);

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
                OrganizationNumber = "313418154",
                CustomizedBody = "Hello $recipientName$ your organization number is $recipientNumber$"
            }
        };

        var organizationDetails = new List<PartyDetails>
        {
            new() { OrganizationNumber = "313418154", Name = "Organization name" }
        };

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync(organizationDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Organization name your organization number is 313418154", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_EmailRecipients_ShouldHandleEmptyLists()
    {
        // Arrange
        var emailRecipients = new List<EmailRecipient>();

        _registerClientMock.Setup(client => client.GetPartyDetails(It.Is<List<string>>(x => x != null && x.Count == 0), It.Is<List<string>>(x => x != null && x.Count == 0)))
            .ReturnsAsync([]);

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

        _registerClientMock.Setup(client => client.GetPartyDetails(It.Is<List<string>>(x => x != null && x.Count == 0), It.Is<List<string>>(x => x != null && x.Count == 0)))
            .ReturnsAsync([]);

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

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync([]);

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

        _registerClientMock.Setup(client => client.GetPartyDetails(It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync([]);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(smsRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello $recipientName$ your number is $recipientNumber$", recipient.CustomizedBody);
    }
}
