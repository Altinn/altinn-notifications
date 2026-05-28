using Altinn.Notifications.Sms.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Sms.Tests.Sms.Core;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCoreServices_NoException()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddCoreServices());

        // Assert
        Assert.Null(exception);
    }
}
