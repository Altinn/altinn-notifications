using System.Security.Claims;
using System.Text.Json;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Notifications.Authorization;

/// <summary>
/// An implementation of <see cref="IAuthorizationService"/> able to check that a potential
/// recipient of a notification can access the resource that the notification is about.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private const string UserIdUrn = "urn:altinn:userid";

    private const string DefaultIssuer = "Altinn";
    private const string ActionCategoryId = "action";
    private const string ResourceCategoryId = "resource";
    private const string AccessSubjectCategoryIdPrefix = "subject";

    private readonly IPDP _pdp;

    /// <summary>
    /// Initialize a new instance the <see cref="AuthorizationService"/> class with the given dependenices.
    /// </summary>
    public AuthorizationService(IPDP pdp)
    {
        _pdp = pdp;
    }

    /// <summary>
    /// An implementation of <see cref="IAuthorizationService.AuthorizeUsersForResource"/> that
    /// will generate an authorization call to Altinn Authorization to check that the given users have read access.
    /// </summary>
    /// <param name="userIds">The list of user ids.</param>
    /// <param name="resourceId">The id of the resource.</param>
    /// <param name="resourceOwnerId">The party id of the resource owner.</param>
    /// <returns>A task</returns>
    public async Task<Dictionary<string, bool>> AuthorizeUsersForResource(List<int> userIds, string resourceId, int resourceOwnerId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = [],
            Action = [CreateActionCategory()],
            Resource = [CreateResourceCategory(resourceId, resourceOwnerId)],
            MultiRequests = new XacmlJsonMultiRequests { RequestReference = [] }
        };

        foreach (int userId in userIds.Distinct())
        {
            XacmlJsonCategory subjectCategory = CreateAccessSubjectCategory(userId);
            request.AccessSubject.Add(subjectCategory);
            request.MultiRequests.RequestReference.Add(CreateRequestReference(subjectCategory.Id));
        }

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        XacmlJsonResponse xacmlJsonResponse = await _pdp.GetDecisionForRequest(jsonRequest);

        Dictionary<string, bool> keyValuePairs = [];

        foreach (var response in xacmlJsonResponse.Response)
        {
            XacmlJsonCategory? xacmlJsonCategory = response.Category.Find(c => c.CategoryId == MatchAttributeCategory.Subject);

            if (xacmlJsonCategory is not null)
            {
                XacmlJsonAttribute? xacmlJsonAttribute = xacmlJsonCategory.Attribute.Find(a => a.AttributeId == UserIdUrn);

                if (xacmlJsonAttribute is not null)
                {
                    keyValuePairs.Add(xacmlJsonAttribute.Value, true);
                }
            }
        }

        return keyValuePairs;
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

    private static XacmlJsonCategory CreateResourceCategory(string resourceId, int resourceOwnerId)
    {
        XacmlJsonAttribute subjectAttribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                AltinnXacmlUrns.PartyId, resourceOwnerId.ToString(), ClaimValueTypes.String, DefaultIssuer);

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
                Id = ResourceCategoryId,
                Attribute = [orgAttribute, appAttribute, subjectAttribute]
            };
        }

        XacmlJsonAttribute resourceAttribute =
            DecisionHelper.CreateXacmlJsonAttribute(
                AltinnXacmlUrns.ResourceId, resourceId, ClaimValueTypes.String, DefaultIssuer);

        return new XacmlJsonCategory()
        {
            Id = ResourceCategoryId,
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

    private static XacmlJsonRequestReference CreateRequestReference(string subjectCategoryId)
    {
        return new XacmlJsonRequestReference
        { 
            ReferenceId = new List<string> 
            { 
                subjectCategoryId, 
                ActionCategoryId, 
                ResourceCategoryId 
            } 
        };
    }
}
