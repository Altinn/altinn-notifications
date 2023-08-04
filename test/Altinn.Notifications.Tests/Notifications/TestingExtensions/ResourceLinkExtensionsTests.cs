using System;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications;
public static class ResourceLinkExtensionsTests
{
    [Fact]
    public static void SetResourceLinks_ThrowsExceptionIfNotInitialized()
    {
        ResourceLinkExtensions.Initialize(null);
        Assert.Throws<InvalidOperationException>(() => { ResourceLinkExtensions.SetResourceLinks(new NotificationOrderExt()); });
    }

    [Fact]
    public static void NotificationSummaryResourceLinkss_ThrowsExceptionIfNotInitialized()
    {
        ResourceLinkExtensions.Initialize(null);
        Assert.Throws<InvalidOperationException>(() => { ResourceLinkExtensions.NotificationSummaryResourceLinks(new NotificationOrderWithStatusExt()); });
    }

    [Fact]
    public static void GetSelfLink_ThrowsExceptionIfNotInitialized()
    {
        ResourceLinkExtensions.Initialize(null);

        Assert.Throws<InvalidOperationException>(() => { ResourceLinkExtensions.GetSelfLink(new NotificationOrder()); });
    }
}

