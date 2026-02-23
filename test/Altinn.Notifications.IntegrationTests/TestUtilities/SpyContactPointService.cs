using System.Collections.Concurrent;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.IntegrationTests.TestUtilities;

/// <summary>
/// Test spy that wraps the real ContactPointService and records OrderPhase values used in method calls.
/// This allows integration tests to verify that the correct OrderPhase is passed through the entire flow.
/// </summary>
public class SpyContactPointService : IContactPointService
{
    private readonly IContactPointService _innerService;

    public ConcurrentBag<(string Method, OrderPhase Phase)> RecordedCalls { get; } = new();

    public SpyContactPointService(IContactPointService innerService)
    {
        _innerService = innerService;
    }

    public async Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddEmailContactPoints", orderPhase));
        await _innerService.AddEmailContactPoints(recipients, resourceId, orderPhase);
    }

    public async Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddSmsContactPoints", orderPhase));
        await _innerService.AddSmsContactPoints(recipients, resourceId, orderPhase);
    }

    public async Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddEmailAndSmsContactPointsAsync", orderPhase));
        await _innerService.AddEmailAndSmsContactPointsAsync(recipients, resourceId, orderPhase);
    }

    public async Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId, OrderPhase orderPhase = OrderPhase.Processing)
    {
        RecordedCalls.Add(("AddPreferredContactPoints", orderPhase));
        await _innerService.AddPreferredContactPoints(channel, recipients, resourceId, orderPhase);
    }

    public void Reset()
    {
        RecordedCalls.Clear();
    }
}
