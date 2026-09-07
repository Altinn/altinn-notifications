#nullable enable
using System.Net;

using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Dialogporten;
using Altinn.Notifications.IntegrationTests;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Dialogporten;

public class DialogportenClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Created, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task CheckUserAccessToDialog_DialogportenReturnsOk_ReturnsTrue(HttpStatusCode statusCode, bool expectedResult)
    {
        // Arrange
        DelegatingHandlerStub delegatingHandlerStub = new((request, token) =>
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        });

        Mock<IAuthenticationContext> mockAuthenticationContext = new();
        mockAuthenticationContext.Setup(ac => ac.GetTokenFromContext()).Returns("valid-token");

        DialogportenClient target = new(
            new HttpClient(delegatingHandlerStub),
            Options.Create(new DialogportenSettings { BaseUrl = "https://example.com", Enabled = true }),
            mockAuthenticationContext.Object);

        // Act
        bool result = await target.CheckUserAccessToDialog(Guid.NewGuid());

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task CheckUserAccessToDialog_DisabledSettings_ReturnsFalse()
    {
        // Arrange
        Mock<IAuthenticationContext> mockAuthenticationContext = new();

        DialogportenClient target = new(
            new HttpClient(),
            Options.Create(new DialogportenSettings { BaseUrl = "https://example.com", Enabled = false }),
            mockAuthenticationContext.Object);

        // Act
        bool result = await target.CheckUserAccessToDialog(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CheckUserAccessToDialog_MissingToken_ReturnsFalse(string? token)
    {
        // Arrange
        Mock<IAuthenticationContext> mockAuthenticationContext = new();
        mockAuthenticationContext.Setup(ac => ac.GetTokenFromContext()).Returns(token);

        DialogportenClient target = new(
            new HttpClient(),
            Options.Create(new DialogportenSettings { BaseUrl = "https://example.com", Enabled = true }),
            mockAuthenticationContext.Object);

        // Act
        bool result = await target.CheckUserAccessToDialog(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }
}
