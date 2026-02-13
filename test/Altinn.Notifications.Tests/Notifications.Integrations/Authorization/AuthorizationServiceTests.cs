using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Integrations.Authorization;
using Altinn.Notifications.Tests.TestData;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Authorization;

public class AuthorizationServiceTests
{
    private readonly Mock<IPDP> _pdpMock = new();

    private AuthorizationService CreateService(int batchSize = 500)
    {
        var config = Options.Create(new NotificationConfig { AuthorizationBatchSize = batchSize });
        return new AuthorizationService(_pdpMock.Object, config);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_PermitAll()
    {
        // Arrange
        var target = CreateService();

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
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        XacmlJsonRequestRoot expectedRequest = await TestDataLoader.Load<XacmlJsonRequestRoot>("PermitAll");
        actualRequest.Should().BeEquivalentTo(expectedRequest);

        actualResult.Should().BeEquivalentTo(organizationContactPoints);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_DenyOne()
    {
        // Arrange
        var target = CreateService();

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
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

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
        var target = CreateService();

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
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

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

    [Fact]
    public async Task AuthorizeUsersForResource_ZeroUsers_ReturnsEmptyClones()
    {
        // Arrange
        var target = CreateService(batchSize: 2);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints { PartyId = 1001, UserContactPoints = [] },
            new OrganizationContactPoints { PartyId = 1002, UserContactPoints = [] }
        ];

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Never);
        result.Should().HaveCount(2);
        result[0].PartyId.Should().Be(1001);
        result[0].UserContactPoints.Should().BeEmpty();
        result[1].PartyId.Should().Be(1002);
        result[1].UserContactPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task AuthorizeUsersForResource_BatchingSplitsIntoMultiplePdpCalls()
    {
        // Arrange: batch size 2, 5 users across 2 orgs → 3 PDP calls
        var target = CreateService(batchSize: 2);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 1001,
                UserContactPoints =
                [
                    new() { UserId = 1 },
                    new() { UserId = 2 },
                    new() { UserId = 3 }
                ]
            },
            new OrganizationContactPoints
            {
                PartyId = 1002,
                UserContactPoints =
                [
                    new() { UserId = 4 },
                    new() { UserId = 5 }
                ]
            }
        ];

        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Returns((XacmlJsonRequestRoot request) =>
            {
                var responses = request.Request.MultiRequests.RequestReference
                    .Select(rr => CreatePermitResult(request, rr))
                    .ToList();

                return Task.FromResult(new XacmlJsonResponse { Response = responses });
            });

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Exactly(3));

        result.Should().HaveCount(2);
        result.Find(o => o.PartyId == 1001)!.UserContactPoints.Should().HaveCount(3);
        result.Find(o => o.PartyId == 1002)!.UserContactPoints.Should().HaveCount(2);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_SingleOrgExceedsBatchSize_SplitsCorrectly()
    {
        // Arrange: batch size 2, single org with 5 users → 3 PDP calls
        var target = CreateService(batchSize: 2);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 1001,
                UserContactPoints =
                [
                    new() { UserId = 1 },
                    new() { UserId = 2 },
                    new() { UserId = 3 },
                    new() { UserId = 4 },
                    new() { UserId = 5 }
                ]
            }
        ];

        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Returns((XacmlJsonRequestRoot request) =>
            {
                var responses = request.Request.MultiRequests.RequestReference
                    .Select(rr => CreatePermitResult(request, rr))
                    .ToList();

                return Task.FromResult(new XacmlJsonResponse { Response = responses });
            });

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Exactly(3));

        result.Should().HaveCount(1);
        result[0].PartyId.Should().Be(1001);
        result[0].UserContactPoints.Should().HaveCount(5);
        result[0].UserContactPoints.Select(u => u.UserId).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_ExactBatchBoundary_SinglePdpCall()
    {
        // Arrange: batch size 3, exactly 3 users → 1 PDP call (single batch path)
        var target = CreateService(batchSize: 3);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 1001,
                UserContactPoints =
                [
                    new() { UserId = 1 },
                    new() { UserId = 2 }
                ]
            },
            new OrganizationContactPoints
            {
                PartyId = 1002,
                UserContactPoints = [new() { UserId = 3 }]
            }
        ];

        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Returns((XacmlJsonRequestRoot request) =>
            {
                var responses = request.Request.MultiRequests.RequestReference
                    .Select(rr => CreatePermitResult(request, rr))
                    .ToList();

                return Task.FromResult(new XacmlJsonResponse { Response = responses });
            });

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Once);

        result.Should().HaveCount(2);
        result.Find(o => o.PartyId == 1001)!.UserContactPoints.Should().HaveCount(2);
        result.Find(o => o.PartyId == 1002)!.UserContactPoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_MixedPermitDenyAcrossBatches()
    {
        // Arrange: batch size 2, 4 users, deny users 2 and 4
        var target = CreateService(batchSize: 2);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 1001,
                UserContactPoints =
                [
                    new() { UserId = 1 },
                    new() { UserId = 2 }
                ]
            },
            new OrganizationContactPoints
            {
                PartyId = 1002,
                UserContactPoints =
                [
                    new() { UserId = 3 },
                    new() { UserId = 4 }
                ]
            }
        ];

        HashSet<int> deniedUserIds = [2, 4];

        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Returns((XacmlJsonRequestRoot request) =>
            {
                var responses = request.Request.MultiRequests.RequestReference
                    .Select(rr =>
                    {
                        string subjectCatId = rr.ReferenceId.First(id => id.StartsWith("subject"));
                        var subjectCat = request.Request.AccessSubject.Find(s => s.Id == subjectCatId);
                        int userId = int.Parse(subjectCat!.Attribute[0].Value);

                        if (deniedUserIds.Contains(userId))
                        {
                            return CreateDenyResult(request, rr);
                        }

                        return CreatePermitResult(request, rr);
                    })
                    .ToList();

                return Task.FromResult(new XacmlJsonResponse { Response = responses });
            });

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Exactly(2));

        result.Should().HaveCount(2);
        result.Find(o => o.PartyId == 1001)!.UserContactPoints.Should().HaveCount(1);
        result.Find(o => o.PartyId == 1001)!.UserContactPoints[0].UserId.Should().Be(1);
        result.Find(o => o.PartyId == 1002)!.UserContactPoints.Should().HaveCount(1);
        result.Find(o => o.PartyId == 1002)!.UserContactPoints[0].UserId.Should().Be(3);
    }

    [Fact]
    public async Task AuthorizeUsersForResource_OrgWithZeroUsersPreservedInBatchResult()
    {
        // Arrange: batch size 2, org with 0 users should be in result even when batching is used
        var target = CreateService(batchSize: 2);

        List<OrganizationContactPoints> organizationContactPoints =
        [
            new OrganizationContactPoints
            {
                PartyId = 1001,
                UserContactPoints =
                [
                    new() { UserId = 1 },
                    new() { UserId = 2 },
                    new() { UserId = 3 }
                ]
            },
            new OrganizationContactPoints
            {
                PartyId = 1002,
                UserContactPoints = []
            }
        ];

        _pdpMock.Setup(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()))
            .Returns((XacmlJsonRequestRoot request) =>
            {
                var responses = request.Request.MultiRequests.RequestReference
                    .Select(rr => CreatePermitResult(request, rr))
                    .ToList();

                return Task.FromResult(new XacmlJsonResponse { Response = responses });
            });

        // Act
        List<OrganizationContactPoints> result =
            await target.AuthorizeUserContactPointsForResource(organizationContactPoints, "app_ttd_apps-test");

        // Assert
        _pdpMock.Verify(pdp => pdp.GetDecisionForRequest(It.IsAny<XacmlJsonRequestRoot>()), Times.Exactly(2));

        result.Should().HaveCount(2);
        result.Find(o => o.PartyId == 1001)!.UserContactPoints.Should().HaveCount(3);
        result.Find(o => o.PartyId == 1002)!.UserContactPoints.Should().BeEmpty();
    }

    private static XacmlJsonResult CreatePermitResult(XacmlJsonRequestRoot request, XacmlJsonRequestReference reference)
    {
        return CreateResult(request, reference, "Permit");
    }

    private static XacmlJsonResult CreateDenyResult(XacmlJsonRequestRoot request, XacmlJsonRequestReference reference)
    {
        return CreateResult(request, reference, "NotApplicable");
    }

    private static XacmlJsonResult CreateResult(XacmlJsonRequestRoot request, XacmlJsonRequestReference reference, string decision)
    {
        string subjectCategoryId = reference.ReferenceId.First(id => id.StartsWith("subject"));
        string resourceCategoryId = reference.ReferenceId.First(id => id.StartsWith("resource"));

        var subjectCategory = request.Request.AccessSubject.Find(s => s.Id == subjectCategoryId);
        var resourceCategory = request.Request.Resource.Find(r => r.Id == resourceCategoryId);

        string userId = subjectCategory!.Attribute.Find(a => a.AttributeId == "urn:altinn:userid")!.Value;
        string partyId = resourceCategory!.Attribute.Find(a => a.AttributeId == "urn:altinn:partyid")!.Value;

        return new XacmlJsonResult
        {
            Decision = decision,
            Category =
            [
                new XacmlJsonCategory
                {
                    CategoryId = "urn:oasis:names:tc:xacml:1.0:subject-category:access-subject",
                    Attribute =
                    [
                        new XacmlJsonAttribute
                        {
                            AttributeId = "urn:altinn:userid",
                            Value = userId,
                            DataType = "http://www.w3.org/2001/XMLSchema#string"
                        }
                    ]
                },
                new XacmlJsonCategory
                {
                    CategoryId = "urn:oasis:names:tc:xacml:3.0:attribute-category:resource",
                    Attribute =
                    [
                        new XacmlJsonAttribute
                        {
                            AttributeId = "urn:altinn:partyid",
                            Value = partyId,
                            DataType = "http://www.w3.org/2001/XMLSchema#string"
                        }
                    ]
                }
            ]
        };
    }
}
