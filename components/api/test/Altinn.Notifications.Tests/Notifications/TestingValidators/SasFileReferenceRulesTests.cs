using System;

using Altinn.Notifications.Validators.Rules;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class SasFileReferenceRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("container/file.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fakesig")]
    public void RelativeUri_RulesReturnFailureWithoutThrowing(string sasUrl)
    {
        // Arrange
        var uri = new Uri(sasUrl, UriKind.RelativeOrAbsolute);

        // Act & Assert
        Assert.False(uri.IsAbsoluteUri);
        Assert.Null(SasFileReferenceRules.ParseSasExpiry(uri));
        Assert.False(SasFileReferenceRules.HasReadPermission(uri));
        Assert.False(SasFileReferenceRules.HasRequiredSasParameters(uri));
        Assert.False(SasFileReferenceRules.IsAzureBlobStorageHost(uri));
        Assert.False(SasFileReferenceRules.IsAbsoluteHttpsUri(uri));
    }
}
