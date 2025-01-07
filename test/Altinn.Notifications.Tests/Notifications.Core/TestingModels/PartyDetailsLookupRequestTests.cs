using System;
using System.Text.Json;
using Altinn.Notifications.Core.Models.Parties;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class PartyDetailsLookupRequestTests
{
    [Fact]
    public void Constructor_WithBothOrganizationNumberAndSocialSecurityNumber_ThrowsArgumentException()
    {
        // Arrange
        var organizationNumber = "314204298";
        var socialSecurityNumber = "09827398702";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupRequest(organizationNumber, socialSecurityNumber));
        Assert.Equal("You can provide either an organization number or a social security number, but not both.", exception.Message);
    }

    [Fact]
    public void Constructor_WithNoParameters_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupRequest());
        Assert.Equal("You must provide either an organization number or a social security number.", exception.Message);
    }

    [Fact]
    public void Constructor_WithOrganizationNumberOnly_SetsOrganizationNumber()
    {
        // Arrange
        var organizationNumber = "314204298";

        // Act
        var request = new PartyDetailsLookupRequest(organizationNumber: organizationNumber);

        // Assert
        Assert.Equal(organizationNumber, request.OrganizationNumber);
        Assert.Null(request.SocialSecurityNumber);
    }

    [Fact]
    public void Constructor_WithSocialSecurityNumberOnly_SetsSocialSecurityNumber()
    {
        // Arrange
        var socialSecurityNumber = "09827398702";

        // Act
        var request = new PartyDetailsLookupRequest(socialSecurityNumber: socialSecurityNumber);

        // Assert
        Assert.Equal(socialSecurityNumber, request.SocialSecurityNumber);
        Assert.Null(request.OrganizationNumber);
    }

    [Fact]
    public void JSONSerialization_WithOrganizationNumber_OnlyIncludesOrganizationNumber()
    {
        // Arrange
        var request = new PartyDetailsLookupRequest(organizationNumber: "314204298");

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.Contains("\"orgNo\":\"314204298\"", json);
        Assert.DoesNotContain("\"ssn\"", json);
    }

    [Fact]
    public void JSONSerialization_WithSocialSecurityNumber_OnlyIncludesSocialSecurityNumber()
    {
        // Arrange
        var request = new PartyDetailsLookupRequest(socialSecurityNumber: "09827398702");

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.Contains("\"ssn\":\"09827398702\"", json);
        Assert.DoesNotContain("\"orgNo\"", json);
    }
}
