using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Integrations.Profile.Models;

namespace Altinn.Notifications.Integrations.Profile.Mappers;

/// <summary>
/// Extension class to map user contact points from DTO to domain model
/// </summary>
public static class UserContactPointsDtoMapperExtension
{
    /// <summary>
    /// Maps the DTO model to domain model
    /// </summary>
    /// <param name="userContactPointDto">This DTO object</param>
    /// <returns>The UserContactPoints object mapped from this DTO object</returns>
    public static UserContactPoints ToUserContactPoint(this UserContactPointsDto userContactPointDto)
    {
        return new UserContactPoints
        {
            UserId = userContactPointDto.UserId ?? 0,
            NationalIdentityNumber = userContactPointDto.NationalIdentityNumber ?? string.Empty,
            IsReserved = userContactPointDto.IsReserved,
            MobileNumber = userContactPointDto.MobileNumber ?? string.Empty,
            Email = userContactPointDto.Email ?? string.Empty
        };
    }
}
