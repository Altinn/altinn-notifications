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
    public void HasRules_RuleWithEmptyPattern_IsIgnored_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = string.Empty,
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
                PhoneNumberPrefixPattern = "^+34",
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
                PhoneNumberPrefixPattern = "^+34",
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
    public void ResolveSender_LiteralPrefixPatternMatchesAndOwnerHasEntry_ReturnsNumericSender()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^+34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("+4775006000", result);
    }

    [Fact]
    public void ResolveSender_RegexPatternMatchesAndOwnerHasEntry_ReturnsNumericSender()
    {
        // Arrange - pattern with alternation requires regex fallback, matches "+34" or "0034"
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^(?:\\+|00)34",
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
    public void ResolveSender_PatternDoesNotMatch_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^+34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        // Act - Norwegian number, does not match the Spanish prefix rule
        var result = service.ResolveSender("Altinn", "+4799990000", "digdir");

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_PatternMatchesButServiceOwnerHasNoEntry_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^+34",
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
                PhoneNumberPrefixPattern = "^+34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["other-owner"] = "+4700000000" }
            },
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^+34",
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
                PhoneNumberPrefixPattern = "^+34",
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
                PhoneNumberPrefixPattern = "^+34",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "   " }
            }
        ]);

        // Act
        var result = service.ResolveSender("Altinn", "+34123456789", "digdir");

        // Assert
        Assert.Equal("Altinn", result);
    }

    [Fact]
    public void ResolveSender_PathologicalRegexPatternTimesOut_TreatedAsNonMatch_ReturnsConfiguredSenderUnchanged()
    {
        // Arrange - classic catastrophic-backtracking pattern (nested quantifiers), requires
        // regex fallback since it contains metacharacters. Combined with a non-matching
        // input long enough to trigger exponential backtracking before hitting the timeout.
        var service = CreateService(
        [
            new SmsSenderSubstitutionRule
            {
                PhoneNumberPrefixPattern = "^(a+)+$",
                NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
            }
        ]);

        var pathologicalInput = new string('a', 40) + "!";

        // Act
        var result = service.ResolveSender("Altinn", pathologicalInput, "digdir");

        // Assert - the timeout is caught internally and treated as a non-match, so the
        // configured sender is returned unchanged rather than the call throwing or hanging.
        Assert.Equal("Altinn", result);
    }

    private static ISmsSenderSubstitutionService CreateService(List<SmsSenderSubstitutionRule> rules)
    {
        var config = new SmsSenderSubstitutionConfig { Rules = rules };
        return new SmsSenderSubstitutionService(Options.Create(config));
    }
}
