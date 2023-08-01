using System.Runtime;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Extensions;

/// <summary>
/// Gets or sets the 
/// </summary>
public static class ResourceLinkExtensions
{
    private static string? _baseUri;

    /// <summary>
    /// Initializes the ResourceLinkHelper with the base URI from settings.
    /// </summary>
    /// <remarks>
    /// Should be called during startup to ensure base url is set
    /// </remarks>
    public static void Initialize(string baseUri)
    {
        _baseUri = baseUri;
    }

    /// <summary>
    /// Sets the resource links on an external notification order
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception if class has not been initialized in Program.cs</exception>
    public static void SetResourceLinks(this NotificationOrderExt order)
    {
        if (_baseUri == null)
        {
            throw new InvalidOperationException("ResourceLinkHelper has not been initialized with the base URI.");
        }

        string self = _baseUri + "/notifications/api/v1/orders/" + order.Id;

        order.Links = new()
        {
            Self = self,
            Status = self + "/status",
            Notifications = self + "/notifications"
        };
    }

    /// <summary>
    /// Sets the resource links on each external notification order in the list
    /// </summary>
    /// <exception cref="InvalidOperationException">Exception if class has not been initialized in Program.cs</exception>
    public static void SetResourceLinks(this NotificationOrderListExt orderList)
    {
        foreach (NotificationOrderExt order in orderList.Orders)
        {
            order.SetResourceLinks();
        }
    }

        /// <summary>
        /// Gets the self link for the provided notification order
        /// </summary>
        /// <exception cref="InvalidOperationException">Exception if class has not been initialized in Program.cs</exception>
        public static string GetSelfLink(this NotificationOrder order)
    {
        if (_baseUri == null)
        {
            throw new InvalidOperationException("ResourceLinkHelper has not been initialized with the base URI.");
        }

        return _baseUri + "/notifications/api/v1/orders/" + order!.Id;
    }
}