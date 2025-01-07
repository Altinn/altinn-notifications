using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Models.Parties;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class PartyDetailsLookupBatchTests
{
    [Fact]
    public void Constructor_DoesNotThrow_WhenBothListsAreProvided()
    {
        // Arrange
        var organizationNumbers = new List<string> { "313556263" };
        var socialSecurityNumbers = new List<string> { "16877298896" };

        // Act
        var batch = new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers);

        // Assert
        Assert.NotNull(batch);
        Assert.NotEmpty(batch.OrganizationNumbers);
        Assert.NotEmpty(batch.SocialSecurityNumbers);
        Assert.Single(batch.OrganizationNumbers, "313556263");
        Assert.Single(batch.SocialSecurityNumbers, "16877298896");
        Assert.Equal(2, batch.PartyDetailsLookupRequestList.Count);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenOrganizationNumbersIsProvided()
    {
        // Arrange
        List<string>? socialSecurityNumbers = null;
        var organizationNumbers = new List<string> { "314727878" };

        // Act
        var batch = new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers);

        // Assert
        Assert.NotNull(batch);
        Assert.NotEmpty(batch.OrganizationNumbers);
        Assert.Single(batch.OrganizationNumbers, "314727878");
        Assert.Empty(batch.SocialSecurityNumbers);
        Assert.Single(batch.PartyDetailsLookupRequestList);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenSocialSecurityNumbersIsProvided()
    {
        // Arrange
        List<string>? organizationNumbers = null;
        var socialSecurityNumbers = new List<string> { "55869600449" };

        // Act
        var batch = new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers);

        // Assert
        Assert.NotNull(batch);
        Assert.NotEmpty(batch.SocialSecurityNumbers);
        Assert.Single(batch.SocialSecurityNumbers, "55869600449");
        Assert.Empty(batch.OrganizationNumbers);
        Assert.Single(batch.PartyDetailsLookupRequestList);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenBothListsAreEmpty()
    {
        // Arrange
        List<string>? organizationNumbers = [];
        List<string>? socialSecurityNumbers = [];

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers));
        Assert.Equal("You must provide either an organization number or a social security number", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenBothListsAreNull()
    {
        // Arrange
        List<string>? organizationNumbers = null;
        List<string>? socialSecurityNumbers = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers));
        Assert.Equal("You must provide either an organization number or a social security number", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenOrganizationNumbersIsEmptyAndSocialSecurityNumbersIsNull()
    {
        // Arrange
        List<string>? organizationNumbers = [];
        List<string>? socialSecurityNumbers = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers));
        Assert.Equal("You must provide either an organization number or a social security number", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenOrganizationNumbersIsNullAndSocialSecurityNumbersIsEmpty()
    {
        // Arrange
        List<string>? organizationNumbers = null;
        List<string>? socialSecurityNumbers = [];

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers));
        Assert.Equal("You must provide either an organization number or a social security number", exception.Message);
    }
}
