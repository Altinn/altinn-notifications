using System.Security.Claims;

using Altinn.Notifications.Extensions;

using AltinnCore.Authentication.Constants;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingExtensions;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetOrg_WhenOrgClaimExists_ReturnsOrgValue()
    {
        // Arrange
        ClaimsPrincipal user = CreatePrincipalWithClaim(AltinnCoreClaimTypes.Org, "ttd");

        // Act
        string? result = user.GetOrg();

        // Assert
        Assert.Equal("ttd", result);
    }

    [Fact]
    public void GetOrg_WhenOrgClaimDoesNotExist_ReturnsNull()
    {
        // Arrange
        ClaimsPrincipal user = CreatePrincipalWithClaim(AltinnCoreClaimTypes.UserId, "12345");

        // Act
        string? result = user.GetOrg();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetOrg_WhenUserIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ClaimsPrincipal user = null!;
        Action act = () => user.GetOrg();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void GetUserId_WhenUserIdClaimExists_ReturnsUserIdValue()
    {
        // Arrange
        ClaimsPrincipal user = CreatePrincipalWithClaim(AltinnCoreClaimTypes.UserId, "12345");

        // Act
        string? result = user.GetUserId();

        // Assert
        Assert.Equal("12345", result);
    }

    [Fact]
    public void GetUserId_WhenUserIdClaimDoesNotExist_ReturnsNull()
    {
        // Arrange
        ClaimsPrincipal user = CreatePrincipalWithClaim(AltinnCoreClaimTypes.Org, "ttd");

        // Act
        string? result = user.GetUserId();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetUserId_WhenUserIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        ClaimsPrincipal user = null!;
        Action act = () => user.GetUserId();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    private static ClaimsPrincipal CreatePrincipalWithClaim(string claimType, string claimValue)
    {
        ClaimsIdentity identity = new([new Claim(claimType, claimValue)], "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
