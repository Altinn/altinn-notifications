using System.Collections.Generic;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Integrations.Authorization;
using Altinn.Notifications.Tests.TestData;

using FluentAssertions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations;

public class AuthorizationClientTests
{
    private Mock<IPDP> _pdpMock = new Mock<IPDP>();

    private AuthorizationClient _target;

    public AuthorizationClientTests()
    {
        _target = new AuthorizationClient(_pdpMock.Object);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_PermitAll()
    {
        // Arrange
        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints 
            { 
                PartyId = 51326783, 
                UserContactPoints = [new() { UserId = 20020164 }]
            },
            new OrganizationContactPoints 
            { 
                PartyId = 51529389, 
                UserContactPoints = [new() { UserId = 20020106 }, new() { UserId = 20020164 }]
            }
        ];

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("PermitAll"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(organizationContactPoints, "app_ttd_apps-test");

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
        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 51326783,
                UserContactPoints = [new() { UserId = 20020164 }]
            },
            new OrganizationContactPoints
            {
                PartyId = 51529389,
                UserContactPoints = [new() { UserId = 20020168 }, new() { UserId = 20020164 }]
            }
        ];

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("DenyOne"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(organizationContactPoints, "app_ttd_apps-test");

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
        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 51326783,
                UserContactPoints = [new() { UserId = 55555555 }]
            },
            new OrganizationContactPoints
            {
                PartyId = 51529389,
                UserContactPoints = [new() { UserId = 66666666 }, new() { UserId = 55555555 }]
            }
        ];

        XacmlJsonRequestRoot? actualRequest = null;
        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Callback((XacmlJsonRequestRoot request) => actualRequest = request)
            .ReturnsAsync(await TestDataLoader.Load<XacmlJsonResponse>("DenyAll"));

        // Act
        Dictionary<string, Dictionary<string, bool>> actualResult =
            await _target.AuthorizeUsersForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("DenyAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        Dictionary<string, Dictionary<string, bool>> expectedResult = []; // Empty
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}
