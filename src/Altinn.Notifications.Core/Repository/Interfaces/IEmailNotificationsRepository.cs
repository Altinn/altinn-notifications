using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Repository.Interfaces;

/// <summary>
/// Interface describing all repository operations related to an email notification
/// </summary>
public interface IEmailNotificationsRepository
{
    /// <summary>
    /// Adds a new email notification to the data base
    /// </summary>
    public Task AddEmailNotification(EmailNotification notification, DateTime expiry);
}