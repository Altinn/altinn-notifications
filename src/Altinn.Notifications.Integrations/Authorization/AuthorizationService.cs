using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Common.PEP.Interfaces;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;

using Microsoft.Extensions.Options;

using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Notifications.Integrations.Authorization;

/// <summary>
/// An implementation of <see cref="IAuthorizationService"/> able to check that a potential
/// recipient of a notification can access the resource that the notification is about.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private const string UserIdUrn = "urn:altinn:userid";

    private const string DefaultIssuer = "Altinn";
    private const string ActionCategoryId = "action";
    private const string ResourceCategoryIdPrefix = "resource";
    private const string AccessSubjectCategoryIdPrefix = "subject";

    private readonly IPDP _pdp;
    private readonly int _authorizationBatchSize;

    /// <summary>
    /// Initialize a new instance the <see cref="AuthorizationService"/> class with the given dependenices.
    /// </summary>
    public AuthorizationService(IPDP pdp, IOptions<NotificationConfig> config)
    {
        _pdp = pdp;
        _authorizationBatchSize = config.Value.AuthorizationBatchSize;
    }

    /// <summary>
    /// An implementation of <see cref="IAuthorizationService.AuthorizeUserContactPointsForResource"/> that
    /// will generate an authorization call to Altinn Authorization to check that the given users have read access.
    /// </summary>
    /// <param name="organizationContactPoints">The list organizations with associated right holders.</param>
    /// <param name="resourceId">The id of the resource.</param>
    /// <returns>A new list of <see cref="OrganizationContactPoints"/> with filtered list of recipients.</returns>
    public async Task<List<OrganizationContactPoints>> AuthorizeUserContactPointsForResource(List<OrganizationContactPoints> organizationContactPoints, string resourceId)
    {
        int totalUsers = organizationContactPoints.Sum(o => o.UserContactPoints.Select(u => u.UserId).Distinct().Count());

        if (totalUsers == 0)
        {
            return organizationContactPoints.Select(o => o.CloneWithoutContactPoints()).ToList();
        }

        if (totalUsers <= _authorizationBatchSize)
        {
            return await AuthorizeSingleBatch(organizationContactPoints, resourceId);
        }

        List<List<OrganizationContactPoints>> batches = CreateBatches(organizationContactPoints);

        Task<List<OrganizationContactPoints>>[] batchTasks = batches
            .Select(batch => AuthorizeSingleBatch(batch, resourceId))
            .ToArray();

        List<OrganizationContactPoints>[] batchResults = await Task.WhenAll(batchTasks);

        return MergeBatchResults(organizationContactPoints, batchResults);
    }

    private async Task<List<OrganizationContactPoints>> AuthorizeSingleBatch(List<OrganizationContactPoints> organizationContactPoints, string resourceId)
    {
        XacmlJsonRequestRoot jsonRequest = BuildAuthorizationRequest(organizationContactPoints, resourceId);

        XacmlJsonResponse xacmlJsonResponse = await _pdp.GetDecisionForRequest(jsonRequest);

        List<OrganizationContactPoints> filtered =
            organizationContactPoints.Select(o => o.CloneWithoutContactPoints()).ToList();

        foreach (var response in xacmlJsonResponse.Response.Where(r => r.Decision == "Permit"))
        {
            string? partyId = GetValue(response, MatchAttributeCategory.Resource, AltinnXacmlUrns.PartyId);
            string? userId = GetValue(response, MatchAttributeCategory.Subject, UserIdUrn);

            if (partyId == null || userId == null)
            {
                continue;
            }

            OrganizationContactPoints? sourceOrg = organizationContactPoints.Find(o => o.PartyId == int.Parse(partyId));
            OrganizationContactPoints? targetOrg = filtered.Find(o => o.PartyId == int.Parse(partyId));

            if (sourceOrg is null || targetOrg is null)
            {
                continue;
            }

            UserContactPoints? user = sourceOrg.UserContactPoints.Find(u => u.UserId == int.Parse(userId));

            if (user is not null)
            {
                targetOrg.UserContactPoints.Add(user.Clone());
            }
        }

        return filtered;
    }

    private List<List<OrganizationContactPoints>> CreateBatches(List<OrganizationContactPoints> organizationContactPoints)
    {
        List<List<OrganizationContactPoints>> batches = [];
        List<OrganizationContactPoints> currentBatch = [];
        int currentBatchCount = 0;

        foreach (var org in organizationContactPoints)
        {
            List<int> distinctUserIds = org.UserContactPoints.Select(u => u.UserId).Distinct().ToList();

            if (distinctUserIds.Count == 0)
            {
                continue;
            }

            int userIndex = 0;

            while (userIndex < distinctUserIds.Count)
            {
                int remaining = _authorizationBatchSize - currentBatchCount;

                if (remaining == 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = [];
                    currentBatchCount = 0;
                    remaining = _authorizationBatchSize;
                }

                int take = Math.Min(remaining, distinctUserIds.Count - userIndex);
                var userIdsForBatch = distinctUserIds.GetRange(userIndex, take);

                var batchOrg = new OrganizationContactPoints
                {
                    OrganizationNumber = org.OrganizationNumber,
                    PartyId = org.PartyId,
                    UserContactPoints = org.UserContactPoints
                        .Where(u => userIdsForBatch.Contains(u.UserId))
                        .ToList()
                };

                currentBatch.Add(batchOrg);
                currentBatchCount += take;
                userIndex += take;
            }
        }

        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }

        return batches;
    }

    private static List<OrganizationContactPoints> MergeBatchResults(
        List<OrganizationContactPoints> originalOrganizations,
        List<OrganizationContactPoints>[] batchResults)
    {
        List<OrganizationContactPoints> merged =
            originalOrganizations.Select(o => o.CloneWithoutContactPoints()).ToList();

        foreach (var batchResult in batchResults)
        {
            foreach (var batchOrg in batchResult)
            {
                OrganizationContactPoints? targetOrg = merged.Find(o => o.PartyId == batchOrg.PartyId);

                if (targetOrg is null)
                {
                    continue;
                }

                var usersToAdd = batchOrg.UserContactPoints
                    .Where(user => !targetOrg.UserContactPoints.Exists(u => u.UserId == user.UserId));

                foreach (var user in usersToAdd)
                {
                    targetOrg.UserContactPoints.Add(user);
                }
            }
        }

        return merged;
    }

    private static XacmlJsonRequestRoot BuildAuthorizationRequest(List<OrganizationContactPoints> organizationContactPoints, string resourceId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = [],
            Action = [CreateActionCategory()],
            Resource = [],
            MultiRequests = new XacmlJsonMultiRequests { RequestReference = [] }
        };

        foreach (var organization in organizationContactPoints)
        {
            XacmlJsonCategory resourceCategory = CreateResourceCategory(organization.PartyId, resourceId);

            if (request.Resource.TrueForAll(rc => rc.Id != resourceCategory.Id))
            {
                request.Resource.Add(resourceCategory);
            }

            foreach (int userId in organization.UserContactPoints.Select(u => u.UserId).Distinct())
            {
                XacmlJsonCategory subjectCategory = CreateAccessSubjectCategory(userId);

                if (request.AccessSubject.TrueForAll(sc => sc.Id != subjectCategory.Id))
                {
                    request.AccessSubject.Add(subjectCategory);
                }

                request.MultiRequests.RequestReference.Add(CreateRequestReference(resourceCategory.Id, subjectCategory.Id));
            }
        }

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };
        return jsonRequest;
    }

    private static XacmlJsonCategory CreateActionCategory()
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

    private static XacmlJsonCategory CreateAccessSubjectCategory(int userId)
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
            ReferenceId =
            [
                subjectCategoryId,
                ActionCategoryId,
                resourceCategoryId
            ]
        };
    }

    private static string? GetValue(XacmlJsonResult response, string categoryId, string attributeId)
    {
        XacmlJsonCategory? resourceCategory =
            response.Category.Find(c => c.CategoryId == categoryId);

        XacmlJsonAttribute? partyAttribute =
            resourceCategory?.Attribute.Find(a => a.AttributeId == attributeId);

        return partyAttribute?.Value;
    }
}
