#nullable enable

using Altinn.Notifications.Authorization;

using Microsoft.AspNetCore.Http;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Authorization;

public class AuthenticationContextTests
{
    [Fact]
    public void GetTokenFromContext_HasAuthorizationHeaderWithBearer_ReturnsToken()
    {
        // Arrange
        HttpContext httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer token";

        Mock<IHttpContextAccessor> mockHttpContextAccessor = new();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var target = new AuthenticationContext(mockHttpContextAccessor.Object);

        // Act
        string? actual = target.GetTokenFromContext();

        // Assert
        Assert.Equal("token", actual);
    }

    [Fact]
    public void GetTokenFromContext_NoHttpContext_ReturnsNull()
    {
        // Arrange
        Mock<IHttpContextAccessor> mockHttpContextAccessor = new();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var target = new AuthenticationContext(mockHttpContextAccessor.Object);

        // Act
        string? actual = target.GetTokenFromContext();

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void GetTokenFromContext_NoAuthorizationHeader_ReturnsNull()
    {
        // Arrange
        HttpContext httpContext = new DefaultHttpContext();

        Mock<IHttpContextAccessor> mockHttpContextAccessor = new();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var target = new AuthenticationContext(mockHttpContextAccessor.Object);

        // Act
        string? actual = target.GetTokenFromContext();

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void GetTokenFromContext_HasAuthorizationHeaderWithBasic_ReturnsToken()
    {
        // Arrange
        HttpContext httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Basic token";

        Mock<IHttpContextAccessor> mockHttpContextAccessor = new();
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var target = new AuthenticationContext(mockHttpContextAccessor.Object);

        // Act
        string? actual = target.GetTokenFromContext();

        // Assert
        Assert.Null(actual);
    }
}
