using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class ApplicationOwnerConfigServiceTests
{
    [Fact]
    public async Task GetApplicationOwnerConfigTest_Repository_returns_found_config_Service_returns_found_config()
    {
        // Arrange
        const string OrgId = "exists";

        Mock<IApplicationOwnerConfigRepository> repositoryMock = new();
        repositoryMock.Setup(a => a.GetApplicationOwnerConfig(It.Is<string>(o => o == OrgId)))
            .ReturnsAsync(new ApplicationOwnerConfig(OrgId));

        ApplicationOwnerConfigService target = new ApplicationOwnerConfigService(repositoryMock.Object);

        // Act
        var actual = await target.GetApplicationOwnerConfig(OrgId);

        // Assert
        repositoryMock.VerifyAll();

        Assert.NotNull(actual);
    }

    [Fact]
    public async Task GetApplicationOwnerConfigTest_Repository_returns_null_Service_returns_empty_config()
    {
        // Arrange
        const string OrgId = "wrong";

        Mock<IApplicationOwnerConfigRepository> repositoryMock = new();
        repositoryMock.Setup(a => a.GetApplicationOwnerConfig(It.Is<string>(o => o == OrgId)))
            .ReturnsAsync((ApplicationOwnerConfig?)null);

        ApplicationOwnerConfigService target = new ApplicationOwnerConfigService(repositoryMock.Object);

        // Act
        var actual = await target.GetApplicationOwnerConfig(OrgId);

        // Assert
        repositoryMock.VerifyAll();

        Assert.NotNull(actual);
    }

    [Fact]
    public async Task WriteApplicationOwnerConfigTest_Repository_returns_null_Service_returns_empty_config()
    {
        // Arrange
        const string OrgId = "exists";

        Mock<IApplicationOwnerConfigRepository> repositoryMock = new();
        repositoryMock.Setup(a => a.WriteApplicationOwnerConfig(It.Is<ApplicationOwnerConfig>(c => c.OrgId == OrgId)));

        ApplicationOwnerConfigService target = new ApplicationOwnerConfigService(repositoryMock.Object);

        // Act
        await target.WriteApplicationOwnerConfig(new ApplicationOwnerConfig(OrgId));

        // Assert
        repositoryMock.VerifyAll();
    }
}
