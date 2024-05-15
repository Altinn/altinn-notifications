using System.Collections.Generic;
using System.Threading.Tasks;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Integrations.Authorization;
using Altinn.Notifications.Tests.TestData;

using FluentAssertions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations;

public class AuthorizationServiceTests
{
    private Mock<IPDP> _pdpMock = new Mock<IPDP>();

    private AuthorizationService _target;

    public AuthorizationServiceTests()
    {
        _target = new AuthorizationService(_pdpMock.Object);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_PermitAll()
    {
        // Arrange
        Dictionary<int, List<int>> input = new()
        {
            { 51326783, new List<int> { 20020164 } },
            { 51529389, new List<int> { 20020106, 20020164 } }
        };

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("PermitAll"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(input, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("PermitAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        Dictionary<string, Dictionary<string, bool>> expectedResult = new()
        {
            { "51326783", new Dictionary<string, bool>() { { "20020164", true } } },
            { "51529389", new Dictionary<string, bool>() { { "20020106", true }, { "20020164", true } } }
        };
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_DenyOne()
    {
        // Arrange
        Dictionary<int, List<int>> input = new()
        {
            { 51326783, new List<int> { 20020164 } },
            { 51529389, new List<int> { 20020168, 20020164 } }
        };

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("DenyOne"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(input, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("DenyOne");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        Dictionary<string, Dictionary<string, bool>> expectedResult = new()
        {
            { "51326783", new Dictionary<string, bool>() { { "20020164", true } } },
            { "51529389", new Dictionary<string, bool>() { { "20020164", true } } }
        };
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_DenyAll()
    {
        // Arrange
        Dictionary<int, List<int>> input = new()
        {
            { 51326783, new List<int> { 55555555 } },
            { 51529389, new List<int> { 55555555, 66666666 } }
        };

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("DenyAll"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(input, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("DenyAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        Dictionary<string, Dictionary<string, bool>> expectedResult = []; // Empty
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}
