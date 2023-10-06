using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class ApplicationOwnerConfigMapperTests
{
    [Fact]
    public void ToApplicationOwnerConfigExtTest_Input_values_match_output_values()
    {
        // Arrange
        ApplicationOwnerConfig source = new("ttd");
        source.EmailAddresses.Add("anemail@address.no");
        source.EmailAddresses.Add("anotheremail@address.no");
        source.SmsNames.Add("97657618");
        source.SmsNames.Add("56543");

        ApplicationOwnerConfigExt expected = new("ttd");
        expected.EmailAddresses.Add("anemail@address.no");
        expected.EmailAddresses.Add("anotheremail@address.no");
        expected.SmsNames.Add("97657618");
        expected.SmsNames.Add("56543");

        // Act
        ApplicationOwnerConfigExt actual = source.ToApplicationOwnerConfigExt();

        // Assert
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public void ToApplicationOwnerConfigTest_Input_values_match_output_values()
    {
        // Arrange
        ApplicationOwnerConfigExt source = new("ttd");
        source.EmailAddresses.Add("anemail@address.no");
        source.EmailAddresses.Add("anotheremail@address.no");
        source.SmsNames.Add("97657618");
        source.SmsNames.Add("56543");

        ApplicationOwnerConfig expected = new("ttd");
        expected.EmailAddresses.Add("anemail@address.no");
        expected.EmailAddresses.Add("anotheremail@address.no");
        expected.SmsNames.Add("97657618");
        expected.SmsNames.Add("56543");

        // Act
        ApplicationOwnerConfig actual = source.ToApplicationOwnerConfig();

        // Assert
        Assert.Equivalent(expected, actual);
    }
}
