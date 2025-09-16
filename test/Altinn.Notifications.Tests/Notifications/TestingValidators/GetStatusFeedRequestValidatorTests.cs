using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Validators;

using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
    public class GetStatusFeedRequestValidatorTests
    {
        private readonly GetStatusFeedRequestValidator _sut = new();

        [Fact]
        public void Should_Have_Validation_Error_For_Seq_When_Negative()
        {
            // arrange
            var request = new GetStatusFeedRequest
            {
                Seq = -1,
            };

            // act
            var actual = _sut.TestValidate(request);

            // assert
            Assert.False(actual.IsValid);
            Assert.Contains(actual.Errors, e => e.PropertyName == "Seq");
            Assert.Contains(actual.Errors, e => e.ErrorMessage == "Sequence number cannot be less than 0.");
        }
    }
}
