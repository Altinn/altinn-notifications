using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;

using static Altinn.Notifications.Core.Models.Orders.NotificationOrder;

namespace Altinn.Notifications.Tests;

public static class TestdataUtil
{
    /// <summary>
    /// Generates a notification order using the default value for each missing property. 
    /// </summary>
    public static NotificationOrder GetOrderForTest(NotificationOrderBuilder builder)
    {
        if (!builder.IdSet)
        {
            builder.SetId(Guid.NewGuid());
        }

        if (!builder.RequestedSendTimeSet)
        {
            builder.SetRequestedSendTime(DateTime.Now);
        }

        if (!builder.NotificationChannelSet)
        {
            builder.SetNotificationChannel(NotificationChannel.Email);
        }

        if (!builder.CreatorSet)
        {
            builder.SetCreator(new Creator("test"));
        }

        if (!builder.CreatedSet)
        {
            builder.SetCreated(DateTime.Now);
        }

        if (!builder.TemplatesSet)
        {
            builder.SetTemplates(new List<INotificationTemplate>());
        }

        if (!builder.RecipientsSet)
        {
            builder.SetRecipients(new List<Recipient>());
        }

        return builder.Build();
    }

    /// <summary>
    /// Generates a notification order using the default value for all properties 
    /// </summary>
    public static NotificationOrder GetOrderForTest()
    {
        return GetOrderForTest(GetBuilder());
    }
}
