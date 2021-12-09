using Altinn.Platform.Profile.Models;

namespace Altinn.Notifications.Core
{
    /// <summary>
    /// Defines the methods required for an implementation of a profile retriever.
    /// </summary>
    public interface IProfileRetriever
    {
        /// <summary>
        /// Defines a method that can retrieve a user profile based on a user id.
        /// </summary>
        /// <param name="userId">The unique id of the profile to retrieve.</param>
        /// <param name="ct">The cancellation token to cancel operation.</param>
        /// <returns>The identified user profile if found.</returns>
        Task<UserProfile?> GetUserProfile(int userId, CancellationToken ct);

        /// <summary>
        /// Defines a method that can retrieve a user profile based on a national identity number.
        /// </summary>
        /// <param name="nationalIdentityNumber">The national identity number of a person.</param>
        /// <param name="ct">The cancellation token to cancel operation.</param>
        /// <returns>The identified user profile if found.</returns>
        Task<UserProfile?> GetUserProfile(string nationalIdentityNumber, CancellationToken ct);
    }
}
