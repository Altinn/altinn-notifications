using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class SmsSenderSubstitutionServiceTests
{
    [Fact]
    public void HasRules_NoRulesConfigured_ReturnsFalse()
    {
        // Arrange
        var service = CreateService([]);

        // Act & Assert
        Assert.False(service.HasRules);
    }

    [Fact]
    public void HasRules_RuleWithEmptyPrefix_IsIgnored_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = string.Empty,
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act & Assert
        Assert.False(service.HasRules);
    }

    [Fact]
    public void HasRules_RuleWithNoServiceOwnerEntries_IsIgnored_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = []
            }
        ]);

        // Act & Assert
        Assert.False(service.HasRules);
    }

    [Fact]
    public void HasRules_ValidRuleConfigured_ReturnsTrue()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act & Assert
        Assert.True(service.HasRules);
    }

    [Fact]
    public void ResolveSender_NoRulesConfigured_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService([]);

        // Act
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_PlusPrefixedNumberMatchesAndOwnerHasEntry_ReturnsNumericSender()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("+4775006000", result);
    }

    [Fact]
    public void ResolveSender_DoubleZeroPrefixedNumberMatchesAndOwnerHasEntry_ReturnsNumericSender()
    {
        // Arrange - "00" and "+" international prefixes must resolve to the same mapping
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act
        var resultWithPlus = service.ResolveSender("Altinn", "+34123456789", "digdir");
        var resultWithZeros = service.ResolveSender("Altinn", "0034123456789", "digdir");

        // Assert
        Assert.Equal("+4775006000", resultWithPlus);
        Assert.Equal("+4775006000", resultWithZeros);
    }

    [Fact]
    public void ResolveSender_PrefixDoesNotMatch_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act - Norwegian number, does not match the Spanish prefix rule
        var result = service.ResolveSender("Altinn", "+4799990000", "digdir");

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_PrefixMatchesButServiceOwnerHasNoEntry_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act - matching prefix, but "other-owner" has no substitution configured
        var result = service.ResolveSender("Altinn", "+34123456789", "other-owner");

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_MultipleRules_FirstMatchingRuleWithOwnerEntryWins()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["other-owner"] = "+4700000000" }
            },
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act - first rule matches the prefix but has no entry for "digdir", second rule does
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("+4775006000", result);
    }

    [Theory]
    [InlineData(null, "digdir")]
    [InlineData("", "digdir")]
    [InlineData(" ", "digdir")]
    [InlineData("+34123456789", null)]
    [InlineData("+34123456789", "")]
    [InlineData("+34123456789", " ")]
    public void ResolveSender_MissingRecipientOrServiceOwner_ReturnsConfiguredSenderUnchanged(string? recipientPhoneNumber, string? serviceOwnerShortName)
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act
        var result = service.ResolveSender("Altinn", recipientPhoneNumber!, serviceOwnerShortName!);

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_NumericSenderIsWhitespaceForMatchingOwner_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                CountryCodePrefix = "34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "   " }
            }
        ]);

        // Act
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("Altinn", result);
    }

    private static SmsSenderSubstitutionService CreateService(List<SmsSenderSubstitutionRule> rules)
    {
        var config = new SmsSenderSubstitutionConfig { Rules = rules };
        return new SmsSenderSubstitutionService(Options.Create(config));
    }
}
