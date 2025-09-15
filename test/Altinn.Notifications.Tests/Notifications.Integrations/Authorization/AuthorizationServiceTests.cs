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

namespace Altinn.Notifications.Tests.Notifications.Integrations.Authorization;

public class AuthorizationServiceTests
{
    private readonly AuthorizationService _target;
    private readonly Mock<IPDP> _pdpMock = new();

    public AuthorizationServiceTests()
    {
        _target = new AuthorizationService(_pdpMock.Object);
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
        List<OrganizationContactPoints> actualResult =
            await _target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("PermitAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        actualResult.Should().BeEquivalentTo(organizationContactPoints);
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
        List<OrganizationContactPoints> actualResult =
            await _target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("DenyOne");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        List<OrganizationContactPoints> expectedResult =
        [
            new OrganizationContactPoints
            {
                PartyId = 51326783,
                UserContactPoints = [new() { UserId = 20020164 }]
            },
            new OrganizationContactPoints
            {
                PartyId = 51529389,
                UserContactPoints = [new() { UserId = 20020164 }]
            }
        ];
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
        List<OrganizationContactPoints> actualResult =
            await _target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("DenyAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        List<OrganizationContactPoints> expectedResult =
        [
            new OrganizationContactPoints { PartyId = 51326783, UserContactPoints = [] },
            new OrganizationContactPoints { PartyId = 51529389, UserContactPoints = [] }
        ];
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}
