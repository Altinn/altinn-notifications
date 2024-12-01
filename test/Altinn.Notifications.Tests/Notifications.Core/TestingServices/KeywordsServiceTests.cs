using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class KeywordsServiceTests
{
    private readonly Mock<IRegisterClient> _registerClientMock;
    private readonly IKeywordsService _keywordsService;

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

        _registerClientMock.Setup(client => client.GetPartyDetailsForPersons(It.IsAny<List<string>>())).ReturnsAsync(personDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Person name", recipient.CustomizedBody);
        Assert.Equal("Subject 07837399275", recipient.CustomizedSubject);
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

        _registerClientMock.Setup(client => client.GetPartyDetailsForOrganizations(It.IsAny<List<string>>())).ReturnsAsync(organizationDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Subject 313997901", recipient.CustomizedSubject);
        Assert.Equal("Hello Organization name", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SmsRecipient_ShouldReplacePlaceholdersForPersons()
    {
        // Arrange
        var emailRecipients = new List<SmsRecipient>
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

        _registerClientMock.Setup(client => client.GetPartyDetailsForPersons(It.IsAny<List<string>>())).ReturnsAsync(personDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Person name your national identity number is 07837399275", recipient.CustomizedBody);
    }

    [Fact]
    public async Task ReplaceKeywordsAsync_SmsRecipients_ShouldReplacePlaceholdersForOrganizations()
    {
        // Arrange
        var emailRecipients = new List<SmsRecipient>
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

        _registerClientMock.Setup(client => client.GetPartyDetailsForOrganizations(It.IsAny<List<string>>())).ReturnsAsync(organizationDetails);

        // Act
        var result = await _keywordsService.ReplaceKeywordsAsync(emailRecipients);

        // Assert
        var recipient = result.First();
        Assert.Equal("Hello Organization name your organization number is 313418154", recipient.CustomizedBody);
    }
}
