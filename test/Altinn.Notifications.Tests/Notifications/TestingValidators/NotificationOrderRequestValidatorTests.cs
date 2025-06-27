using System.Collections.Generic;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;
using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
    public class NotificationOrderRequestValidatorTests
    {
        private readonly NotificationOrderRequestValidator _validator;
        private readonly NotificationOrderRequestExt _validEmailOrder;
        private readonly NotificationOrderRequestExt _validSmsOrder;
        private readonly NotificationOrderRequestExt _validEmailPreferredOrder;
        private readonly NotificationOrderRequestExt _validSmsPreferredOrder;
        private readonly NotificationOrderRequestExt _invalidSmsPreferredOrder;

        public NotificationOrderRequestValidatorTests()
        {
            ValidatorOptions.Global.LanguageManager.Enabled = false;
            _validator = new NotificationOrderRequestValidator();
            _validEmailOrder = new()
            {
                NotificationChannel = NotificationChannelExt.Email,
                EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body" },
                Recipients = [new RecipientExt { EmailAddress = "test@test.com" }]
            };

            _validSmsOrder = new()
            {
                NotificationChannel = NotificationChannelExt.Sms,
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { MobileNumber = "+4799999999" }]
            };

            _validEmailPreferredOrder = new()
            {
                NotificationChannel = NotificationChannelExt.EmailPreferred,
                EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body" },
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { NationalIdentityNumber = "16069412345" }]
            };

            _validSmsPreferredOrder = new()
            {
                NotificationChannel = NotificationChannelExt.SmsPreferred,
                EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body" },
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { OrganizationNumber = "123456789" }]
            };

            _invalidSmsPreferredOrder = new()
            {
                NotificationChannel = NotificationChannelExt.SmsPreferred,
                EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body" },
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { MobileNumber = "+47invalidChar" }]
            };
        }

        [Fact]
        public void Validate_NotificationChannelIsNull_IsNotValid()
        {
            // Arrange
            NotificationOrderRequestExt model = new();

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, a => a.ErrorMessage.Equals("A notification channel must be defined."));
        }

        [Fact]
        public void Validate_Sms_AllRequiredProps_IsValid()
        {
            // Arrange
            var model = _validSmsOrder;

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(" +4799999999")]
        [InlineData(" +47 99999999")]
        [InlineData(" +47 9999 9999")]
        [InlineData(" +47 99 99 99 99")]
        public void Validate_Sms_MobileNumberWithLeadingSpace_IsInvalid(string invalidPhoneNumber)
        {
            // Arrange
            var model = new NotificationOrderRequestExt()
            {
                NotificationChannel = NotificationChannelExt.Sms,
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { MobileNumber = invalidPhoneNumber }]
            };

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("+47 99999999")]
        [InlineData("+47 9999 9999")]
        [InlineData("+47 99 99 99 99")]
        [InlineData("+47 99 99 99 99 ")]
        public void Validate_Sms_MobileNumberContainingNonLeadingSpaces_IsValid(string validPhoneNumber)
        {
            // Arrange
            var model = new NotificationOrderRequestExt()
            {
                NotificationChannel = NotificationChannelExt.Sms,
                SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
                Recipients = [new RecipientExt { MobileNumber = validPhoneNumber }]
            };

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_Email_AllRequiredProps_IsValid()
        {
            // Arrange
            var model = _validEmailOrder;

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_SmsPreferred_AllRequiredProps_IsValid()
        {
            // Arrange
            var model = _validSmsPreferredOrder;

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_EmailPreferred_AllRequiredProps_IsValid()
        {
            // Arrange
            var model = _validEmailPreferredOrder;

            // Act
            var result = _validator.Validate(model);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_EmailPreferred_IdentityNumberCombinedWithMobile_IsNotValid()
        {
            // Arrange
            var model = _validEmailPreferredOrder;
            model.Recipients[0].MobileNumber = "+4799999999";

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, a => a.ErrorMessage.Equals("National identity number cannot be combined with email address, mobile number, or organization number."));
        }

        [Fact]
        public void Validate_SmsPreferred_OrgNumberCombinedWithEmail_IsNotValid()
        {
            // Arrange
            var model = _validSmsPreferredOrder;
            model.Recipients[0].EmailAddress = "test@test.com";

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, a => a.ErrorMessage.Equals("Organization number cannot be combined with email address, mobile number, or national identity number."));
        }

        [Fact]
        public void Validate_SmsPreferred_LettersInMobileNumber_IsNotValid()
        {
            // Arrange
            var model = _invalidSmsPreferredOrder;

            // Act
            var result = _validator.TestValidate(model);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, a => a.ErrorMessage.Equals("Mobile number can contain only '+' and numeric characters, and it must adhere to the E.164 standard."));
        }
    }
}
