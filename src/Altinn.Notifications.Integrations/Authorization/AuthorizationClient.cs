using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Integrations;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Notifications.Integrations.Authorization;

/// <summary>
/// An implementation of <see cref="IAuthorizationService"/> able to check that a potential
/// recipient of a notification can access the resource that the notification is about.
/// </summary>
public class AuthorizationClient : IAuthorizationService
{
    private const string UserIdUrn = "urn:altinn:userid";

    private const string DefaultIssuer = "Altinn";
    private const string ActionCategoryId = "action";
    private const string ResourceCategoryIdPrefix = "resource";
    private const string AccessSubjectCategoryIdPrefix = "subject";

    private readonly IPDP _pdp;

    /// <summary>
    /// Initialize a new instance the <see cref="AuthorizationClient"/> class with the given dependenices.
    /// </summary>
    public AuthorizationClient(IPDP pdp)
    {
        _pdp = pdp;
    }

    /// <summary>
    /// An implementation of <see cref="IAuthorizationService.AuthorizeUsersForResource"/> that
    /// will generate an authorization call to Altinn Authorization to check that the given users have read access.
    /// </summary>
    /// <param name="orgRightHolders">The list organizations with associated right holders.</param>
    /// <param name="resourceId">The id of the resource.</param>
    /// <returns>A task</returns>
    public async Task<Dictionary<string, Dictionary<string, bool>>> AuthorizeUsersForResource(Dictionary<int, List<int>> orgRightHolders, string resourceId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = [],
            Action = [CreateActionCategory()],
            Resource = [],
            MultiRequests = new XacmlJsonMultiRequests { RequestReference = [] }
        };

        foreach (var organization in orgRightHolders)
        {
            XacmlJsonCategory resourceCategory = CreateResourceCategory(organization.Key, resourceId);

            if (request.Resource.All(rc => rc.Id != resourceCategory.Id))
            {
                request.Resource.Add(resourceCategory);
            }

            foreach (int userId in organization.Value.Distinct())
            {
                XacmlJsonCategory subjectCategory = CreateAccessSubjectCategory(userId);

                if (request.AccessSubject.All(sc => sc.Id != subjectCategory.Id))
                {
                    request.AccessSubject.Add(subjectCategory);
                }

                request.MultiRequests.RequestReference.Add(CreateRequestReference(resourceCategory.Id, subjectCategory.Id));
            }
        }

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        XacmlJsonResponse xacmlJsonResponse = await _pdp.GetDecisionForRequest(jsonRequest);

        Dictionary<string, Dictionary<string, bool>> permit = [];

        foreach (var response in xacmlJsonResponse.Response.Where(r => r.Decision == "Permit"))
        {
            XacmlJsonCategory? resourceCategory =
                response.Category.Find(c => c.CategoryId == MatchAttributeCategory.Resource);

            string? partyId = null;

            if (resourceCategory is not null)
            {
                XacmlJsonAttribute? partyAttribute =
                    resourceCategory.Attribute.Find(a => a.AttributeId == AltinnXacmlUrns.PartyId);

                if (partyAttribute is not null)
                {
                    partyId = partyAttribute.Value;
                }
            }

            XacmlJsonCategory? subjectCategory =
                response.Category.Find(c => c.CategoryId == MatchAttributeCategory.Subject);

            if (subjectCategory is not null)
            {
                XacmlJsonAttribute? userAttribute
                    = subjectCategory.Attribute.Find(a => a.AttributeId == UserIdUrn);

                if (userAttribute is not null && partyId is not null)
                {
                    if (permit.TryGetValue(partyId, out Dictionary<string, bool>? value))
                    {
                        value.Add(userAttribute.Value, true);
                    }
                    else
                    {
                        permit.Add(partyId, new Dictionary<string, bool> { { userAttribute.Value, true } });
                    }
                }
            }
        }

        return permit;
    }

    private XacmlJsonCategory CreateActionCategory()
    {
        XacmlJsonAttribute attribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                MatchAttributeIdentifiers.ActionId, "read", "string", DefaultIssuer);

        return new XacmlJsonCategory()
        {
            Id = ActionCategoryId,
            Attribute = [attribute]
        };
    }

    private static XacmlJsonCategory CreateResourceCategory(int resourceOwnerId, string resourceId)
    {
        XacmlJsonAttribute subjectAttribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                AltinnXacmlUrns.PartyId, resourceOwnerId.ToString(), ClaimValueTypes.String, DefaultIssuer, true);

        string resourceCategoryId = ResourceCategoryIdPrefix + resourceOwnerId;

        if (resourceId.StartsWith("app_"))
        {
            string[] appResource = resourceId.Split('_');

            XacmlJsonAttribute orgAttribute =
                DecisionHelper.CreateXacmlJsonAttribute(
                    AltinnXacmlUrns.OrgId, appResource[1], ClaimValueTypes.String, DefaultIssuer);

            XacmlJsonAttribute appAttribute =
                DecisionHelper.CreateXacmlJsonAttribute(
                    AltinnXacmlUrns.AppId, appResource[2], ClaimValueTypes.String, DefaultIssuer);

            return new XacmlJsonCategory()
            {
                Id = resourceCategoryId,
                Attribute = [orgAttribute, appAttribute, subjectAttribute]
            };
        }

        XacmlJsonAttribute resourceAttribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                AltinnXacmlUrns.ResourceId, resourceId, ClaimValueTypes.String, DefaultIssuer);

        return new XacmlJsonCategory()
        {
            Id = resourceCategoryId,
            Attribute = [resourceAttribute, subjectAttribute]
        };
    }

    private XacmlJsonCategory CreateAccessSubjectCategory(int userId)
    {
        XacmlJsonAttribute attribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                UserIdUrn, userId.ToString(), ClaimValueTypes.String, DefaultIssuer, true);

        return new XacmlJsonCategory()
        {
            Id = AccessSubjectCategoryIdPrefix + userId,
            Attribute = [attribute]
        };
    }

    private static XacmlJsonRequestReference CreateRequestReference(string resourceCategoryId, string subjectCategoryId)
    {
        return new XacmlJsonRequestReference
        {
            ReferenceId = new List<string>
            {
                subjectCategoryId,
                ActionCategoryId,
                resourceCategoryId
            }
        };
    }
}
