using System.Collections.Generic;

using Altinn.Notifications.Core.Configuration;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingConfiguration;

public class SmsSenderSubstitutionConfigValidatorTests
{
    private readonly SmsSenderSubstitutionConfigValidator _validator = new();

    [Fact]
    public void Validate_NoRulesConfigured_ReturnsSuccess()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig { Rules = [] };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_PrefixIsNullOrWhitespace_IsIgnored_ReturnsSuccess(string? prefix)
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules = [new SmsSenderSubstitutionRule { CountryCodePrefix = prefix! }]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("47")]
    [InlineData("999")]
    public void Validate_PrefixIsOneToThreeDigits_ReturnsSuccess(string prefix)
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules = [new SmsSenderSubstitutionRule { CountryCodePrefix = prefix }]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_PrefixIsFourDigits_ReturnsFailure()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules = [new SmsSenderSubstitutionRule { CountryCodePrefix = "4700" }]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("4700"));
    }

    [Theory]
    [InlineData("+47")]
    [InlineData("0047")]
    [InlineData("47a")]
    [InlineData("4-7")]
    [InlineData("47.")]
    public void Validate_PrefixContainsNonDigitCharacters_ReturnsFailure(string prefix)
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules = [new SmsSenderSubstitutionRule { CountryCodePrefix = prefix }]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains(prefix));
    }

    [Fact]
    public void Validate_MultipleRulesWithMixedValidity_AggregatesAllFailuresOnly()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule { CountryCodePrefix = "47" },
                new SmsSenderSubstitutionRule { CountryCodePrefix = "+46" },
                new SmsSenderSubstitutionRule { CountryCodePrefix = string.Empty },
                new SmsSenderSubstitutionRule { CountryCodePrefix = "12345" }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Failures!.Count());
        Assert.Contains(result.Failures!, f => f.Contains("+46"));
        Assert.Contains(result.Failures!, f => f.Contains("12345"));
    }

    [Fact]
    public void Validate_AllRulesValid_ReturnsSuccess()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule { CountryCodePrefix = "47" },
                new SmsSenderSubstitutionRule { CountryCodePrefix = "46" },
                new SmsSenderSubstitutionRule { CountryCodePrefix = string.Empty }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NumericSenderIsNullOrWhitespace_IsIgnored_ReturnsSuccess(string? numericSender)
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule
                {
                    CountryCodePrefix = "47",
                    NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = numericSender! }
                }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NumericSenderIsValidMobileNumber_ReturnsSuccess()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule
                {
                    CountryCodePrefix = "47",
                    NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = "+4775006000" }
                }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("12345")]
    [InlineData("4775006000")]
    public void Validate_NumericSenderIsNotAValidMobileNumber_ReturnsFailure(string numericSender)
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule
                {
                    CountryCodePrefix = "47",
                    NumericSenderByServiceOwner = new Dictionary<string, string> { ["digdir"] = numericSender }
                }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains(numericSender) && f.Contains("digdir"));
    }

    [Fact]
    public void Validate_MultipleServiceOwnersWithMixedNumericSenderValidity_AggregatesAllFailuresOnly()
    {
        // Arrange
        var options = new SmsSenderSubstitutionConfig
        {
            Rules =
            [
                new SmsSenderSubstitutionRule
                {
                    CountryCodePrefix = "47",
                    NumericSenderByServiceOwner = new Dictionary<string, string>
                    {
                        ["digdir"] = "+4775006000",
                        ["other-owner"] = "invalid-number"
                    }
                }
            ]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Single(result.Failures!);
        Assert.Contains(result.Failures!, f => f.Contains("invalid-number") && f.Contains("other-owner"));
    }
}
