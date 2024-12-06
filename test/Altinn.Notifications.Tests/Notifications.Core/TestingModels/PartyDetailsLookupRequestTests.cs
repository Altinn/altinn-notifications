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
        Assert.Equal("You can specify either an OrganizationNumber or a SocialSecurityNumber, but not both.", exception.Message);
    }

    [Fact]
    public void Constructor_WithNoParameters_SetsBothPropertiesToNull()
    {
        // Act
        var request = new PartyDetailsLookupRequest();

        // Assert
        Assert.Null(request.OrganizationNumber);
        Assert.Null(request.SocialSecurityNumber);
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
    public void JsonSerialization_WithNullProperties_ExcludesNullProperties()
    {
        // Arrange
        var request = new PartyDetailsLookupRequest();

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.DoesNotContain("\"orgNo\"", json);
        Assert.DoesNotContain("\"ssn\"", json);
        Assert.Equal("{}", json);
    }

    [Fact]
    public void JsonSerialization_WithOrganizationNumber_OnlyIncludesOrganizationNumber()
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
    public void JsonSerialization_WithSocialSecurityNumber_OnlyIncludesSocialSecurityNumber()
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
