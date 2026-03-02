using System.Collections.Concurrent;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Test spy that records OrderPhase values and populates dummy contact points
/// so the downstream missing-contact validation passes.
/// </summary>
public class SpyContactPointService : IContactPointService
{
    public ConcurrentBag<(string Method, OrderPhase Phase)> RecordedCalls { get; } = new();

    public Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddEmailContactPoints", orderPhase));

        foreach (var recipient in recipients)
        {
            recipient.AddressInfo.Add(new EmailAddressPoint("spy@test.local"));
        }

        return Task.CompletedTask;
    }

    public Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddSmsContactPoints", orderPhase));

        foreach (var recipient in recipients)
        {
            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
        }

        return Task.CompletedTask;
    }

    public Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddEmailAndSmsContactPointsAsync", orderPhase));

        foreach (var recipient in recipients)
        {
            recipient.AddressInfo.Add(new EmailAddressPoint("spy@test.local"));
            recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
        }

        return Task.CompletedTask;
    }

    public Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddPreferredContactPoints", orderPhase));

        foreach (var recipient in recipients)
        {
            if (channel == NotificationChannel.Sms)
            {
                recipient.AddressInfo.Add(new SmsAddressPoint("+4799999999"));
            }
            else
            {
                recipient.AddressInfo.Add(new EmailAddressPoint("spy@test.local"));
            }
        }

        return Task.CompletedTask;
    }


    public void Reset()
    {
        RecordedCalls.Clear();
    }
}
