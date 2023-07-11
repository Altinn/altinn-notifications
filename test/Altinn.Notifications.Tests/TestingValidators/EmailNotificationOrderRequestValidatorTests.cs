using System.Collections.Generic;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using Xunit;

namespace Altinn.Notifications.Tests.TestingValidators
{
    public class EmailNotificationOrderRequestValidatorTests
    {
        private readonly EmailNotificationOrderRequestValidator _validator;

        public EmailNotificationOrderRequestValidatorTests()
        {
            _validator = new EmailNotificationOrderRequestValidator();
        }

        [Fact]
        public void Validate_AllRequiredPropsOresent_ReturnsTrue()
        {
            var order = new EmailNotificationOrderRequest()
            {
                Subject = "This is an email subject",
                FromAddress = "sender@domain.com",
                Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
                Body = "This is an email body"
            };

            var actual = _validator.Validate(order);
            Assert.True(actual.IsValid);
        }

        [Fact]
        public void Validate_BothRecipientsAndToAddressPopulated_ReturnsFalse()
        {
            var order = new EmailNotificationOrderRequest()
            {
                FromAddress = "sender@domain.com",
                ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" },
                Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
                Body = "This is an email body"

            };

            var actual = _validator.Validate(order);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void Validate_FromAddressMissing_ReturnsFalse()
        {
            var order = new EmailNotificationOrderRequest()
            {
                Subject = "This is an email subject",
                Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
                Body = "This is an email body"

            };

            var actual = _validator.Validate(order);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void Validate_SubjectMissing_ReturnsFalse()
        {
            var order = new EmailNotificationOrderRequest()
            {
                FromAddress = "sender@domain.com",
                Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
                Body = "This is an email body"
            };

            var actual = _validator.Validate(order);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void Validate_BodyMissing_ReturnsFalse()
        {
            var order = new EmailNotificationOrderRequest()
            {
                Subject = "This is an email subject",
                FromAddress = "sender@domain.com",
                Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
            };

            var actual = _validator.Validate(order);
            Assert.False(actual.IsValid);
        }
    }
}
