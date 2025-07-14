using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class ShortMessageContentValidatorTests
{
    private readonly ShortMessageContentValidator _validator;

    public ShortMessageContentValidatorTests()
    {
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        _validator = new ShortMessageContentValidator();
    }

    [Fact]
    public void Validate_ValidMessageContent_ShouldPass()
    {
        // Arrange
        var content = new ShortMessageContentExt
        {
            Sender = "Test sender",
            Body = "This is a test message"
        };

        // Act
        var result = _validator.Validate(content);

        // Assert
        Assert.True(result.IsValid);
    }
}
