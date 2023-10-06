using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Mapper methods for mapping between <see cref="ApplicationOwnerConfig"/> and 
/// <see cref="ApplicationOwnerConfigExt"/>. The methods are implemented as extension methods for the two classes.
/// </summary>
public static class ApplicationOwnerConfigMapper
{
    /// <summary>
    /// Methods for mapping of data from an <see cref="ApplicationOwnerConfig"/> instance into 
    /// an <see cref="ApplicationOwnerConfigExt"/> instance.
    /// </summary>
    /// <param name="source">An instance of <see cref="ApplicationOwnerConfig"/> to be mapped.</param>
    /// <returns>A new instance of <see cref="ApplicationOwnerConfigExt"/> with values from the given source.</returns>
    public static ApplicationOwnerConfigExt ToApplicationOwnerConfigExt(this ApplicationOwnerConfig source)
    {
        ApplicationOwnerConfigExt target = new(source.OrgId);
        target.EmailAddresses.AddRange(source.EmailAddresses);
        target.SmsNames.AddRange(source.SmsNames);
        return target;
    }

    /// <summary>
    /// Methods for mapping of data from an <see cref="ApplicationOwnerConfigExt"/> instance into 
    /// an <see cref="ApplicationOwnerConfig"/> instance.
    /// </summary>
    /// <param name="source">An instance of <see cref="ApplicationOwnerConfigExt"/> to be mapped.</param>
    /// <returns>A new instance of <see cref="ApplicationOwnerConfig"/> with values from the given source.</returns>
    public static ApplicationOwnerConfig ToApplicationOwnerConfig(this ApplicationOwnerConfigExt source)
    {
        ApplicationOwnerConfig target = new(source.OrgId);
        target.EmailAddresses.AddRange(source.EmailAddresses);
        target.SmsNames.AddRange(source.SmsNames);
        return target;
    }
}
