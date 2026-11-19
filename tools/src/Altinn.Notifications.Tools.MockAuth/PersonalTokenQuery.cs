namespace Altinn.Notifications.Tools.MockAuth;

/// <summary>
/// The query of <c>/api/GetPersonalToken</c>, with the same names as in the Altinn test token generator.
/// Every value is optional, and the issuer fills in a test user for those that are left out.
/// </summary>
/// <param name="UserId">The user's id in Altinn.</param>
/// <param name="PartyId">The user's party id.</param>
/// <param name="Pid">The user's national identity number.</param>
/// <param name="AuthLvl">The authentication level.</param>
/// <param name="Scopes">The scopes, separated by spaces.</param>
internal sealed record PersonalTokenQuery(int? UserId, int? PartyId, string? Pid, int? AuthLvl, string? Scopes);
