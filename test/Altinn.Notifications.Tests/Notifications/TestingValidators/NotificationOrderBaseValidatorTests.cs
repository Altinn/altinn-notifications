using System;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
    public class NotificationOrderBaseValidatorTests
    {
        private readonly NotificationOrderBaseValidator _validator = new();

        [Fact]
        public void Should_Allow_ConditionEndpoint_To_Be_Null()
        {
            // Arrange
            var notificationOrder = new NotificationOrderBaseExt
            {
                ConditionEndpoint = null,
                RequestedSendTime = DateTime.UtcNow.AddMinutes(1)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ConditionEndpoint);
        }

        [Fact]
        public void Should_Fail_Validation_With_Relative_Uri_ConditionEndpoint()
        {
            // Arrange
            var notificationOrder = new NotificationOrderBaseExt
            {
                ConditionEndpoint = new Uri("api/endpoint", UriKind.Relative),
                RequestedSendTime = DateTime.UtcNow.AddMinutes(1)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ConditionEndpoint)
                .WithErrorMessage("ConditionEndpoint must be a valid absolute URI or null.");
        }

        [Fact]
        public void Should_Fail_Validation_When_ConditionEndpoint_Has_Wrong_Protocol()
        {
            // Arrange
            var notificationOrder = new NotificationOrderBaseExt
            {
                ConditionEndpoint = new Uri("urn:isbn:0-321-76572-0", UriKind.Absolute),
                RequestedSendTime = DateTime.UtcNow.AddMinutes(1)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);
            
            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ConditionEndpoint)
                .WithErrorMessage("ConditionEndpoint must use http or https scheme.");
        }

        [Fact]
        public void Should_Fail_Validation_When_TimeZone_Is_Unspecified()
        {
            // Arrange
            var year = DateTime.Now.Year;

            var notificationOrder = new NotificationOrderBaseExt
            {
                RequestedSendTime = new DateTime(year + 1, 10, 1, 12, 0, 0)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RequestedSendTime)
                .WithErrorMessage("The requested send time value must have specified a time zone.");
        }

        [Fact]
        public void Should_Pass_Validation_When_TimeZone_is_Specified()
        {
            // Arrange
            var year = DateTime.Now.Year;

            var notificationOrder = new NotificationOrderBaseExt
            {
                RequestedSendTime = new DateTime(year + 1, 10, 1, 12, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.RequestedSendTime);
        }

        [Fact]
        public void Should_Fail_Validation_When_RequestedSendTime_Is_In_Past()
        {
            // Arrange
            var notificationOrder = new NotificationOrderBaseExt
            {
                RequestedSendTime = DateTime.UtcNow.AddMinutes(-5)
            };

            // Act
            var result = _validator.TestValidate(notificationOrder);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RequestedSendTime)
                .WithErrorMessage("RequestedSendTime must be greater than or equal to now.");
        }
    }
}
