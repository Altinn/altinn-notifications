namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Interface for handling actions towards the resource limits table
    /// </summary>
    public interface IResourceLimitRepository
    {
        /// <summary>
        /// Sets the timeout flag for a given resource
        /// </summary>
        /// <param name="timeout">The date time for when the resource limit is reset</param>
        /// <returns>A boolean indicating if the operation was successful</returns>
        Task<bool> SetEmailTimeout(DateTime timeout);
    }
}
