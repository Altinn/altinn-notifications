using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Validators;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingHelpers;

public class MobileNumberHelperTests
{
    [Theory]
    [InlineData("99315000", "+4799315000")]
    [InlineData("+4799315000", "+4799315000")]
    [InlineData("4123", "4123")]
    [InlineData("+4699999999", "+4699999999")]
    [InlineData("00233517846", "+233517846")]
    [InlineData("81549300", "81549300")]

    public void EnsureCountryCodeIfValidNumber(string input, string expectedOutput)
    {
        string actual = MobileNumberHelper.EnsureCountryCodeIfValidNumber(input);
        Assert.Equal(expectedOutput, actual);
    }

    [Theory]
    [InlineData("+4740000001", true)]
    [InlineData("004740000000", true)]
    [InlineData("40000001", false)]
    [InlineData("90000000", false)]
    [InlineData("+4790000000", true)]
    [InlineData("+4750000004", false)]
    [InlineData("+47900000001", false)]
    [InlineData("+14790000000", false)]
    [InlineData("004790000002", true)]
    [InlineData("", false)]
    [InlineData("111100000", false)]
    [InlineData("dasdsadSASA", false)]
    [InlineData("+233242426224", true)]
    public void IsValidMobileNumber(string mobileNumber, bool expectedResult)
    {
        bool actual = MobileNumberHelper.IsValidMobileNumber(mobileNumber);
        Assert.Equal(expectedResult, actual);
    }
}
