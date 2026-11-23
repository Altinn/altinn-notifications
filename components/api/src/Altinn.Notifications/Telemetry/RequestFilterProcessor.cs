using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

using Altinn.AccessManagement.Core.Models;
using AltinnCore.Authentication.Constants;

using SharedTelemetry = Altinn.Notifications.Shared.Telemetry;

namespace Altinn.Notifications.Telemetry
{
    /// <summary>
    /// Excludes health probe requests and tags request activities with the caller's claims
    /// on top of the shared request filter.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public class RequestFilterProcessor(IHttpContextAccessor httpContextAccessor) : SharedTelemetry.RequestFilterProcessor(httpContextAccessor)
    {
        private static readonly FrozenDictionary<string, Action<Claim, Activity>> _claimActions = InitClaimActions();

        /// <inheritdoc />
        public override void OnEnd(Activity data)
        {
            base.OnEnd(data);

            if (data.OperationName != RequestKind || HttpContextAccessor?.HttpContext is null)
            {
                return;
            }

            foreach (var claim in HttpContextAccessor.HttpContext.User.Claims)
            {
                if (_claimActions.TryGetValue(claim.Type, out var action))
                {
                    action(claim, data);
                }
            }
        }

        private static FrozenDictionary<string, Action<Claim, Activity>> InitClaimActions()
        {
            var actions = new Dictionary<string, Action<Claim, Activity>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    AltinnCoreClaimTypes.UserId,
                    static (claim, activity) => activity.SetTag("user.id", claim.Value)
                },
                {
                    AltinnCoreClaimTypes.PartyID,
                    static (claim, activity) => activity.SetTag("user.party.id", claim.Value)
                },
                {
                    AltinnCoreClaimTypes.AuthenticationLevel,
                    static (claim, activity) => activity.SetTag("user.authentication.level", claim.Value)
                },
                {
                    AltinnCoreClaimTypes.Org,
                    static (claim, activity) => activity.SetTag("user.application.owner.id", claim.Value)
                },
                {
                    AltinnCoreClaimTypes.OrgNumber,
                    static (claim, activity) => activity.SetTag("user.organization.number", claim.Value)
                },
                {
                    "authorization_details",
                    static (claim, activity) =>
                    {
                        SystemUserClaim? claimValue = JsonSerializer.Deserialize<SystemUserClaim>(claim.Value);
                        activity.SetTag("user.system.id", claimValue?.Systemuser_id[0] ?? null);
                        activity.SetTag("user.system.owner.number", claimValue?.Systemuser_org.ID ?? null);
                    }
                },
            };

            return actions.ToFrozenDictionary();
        }
    }
}
