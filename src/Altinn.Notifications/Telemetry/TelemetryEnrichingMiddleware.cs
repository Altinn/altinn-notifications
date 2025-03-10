﻿using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Altinn.AccessManagement.Core.Models;
using AltinnCore.Authentication.Constants;
using Microsoft.AspNetCore.Http.Features;

namespace Altinn.Notifications.Telemetry;

/// <summary>
/// Middleware for enriching telemetry with user claims and route values.
/// </summary>
internal sealed class TelemetryEnrichingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TelemetryEnrichingMiddleware> _logger;
    private static readonly FrozenDictionary<string, Action<Claim, Activity>> _claimActions = InitClaimActions();

    private static FrozenDictionary<string, Action<Claim, Activity>> InitClaimActions()
    {
        var actions = new Dictionary<string, Action<Claim, Activity>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                AltinnCoreClaimTypes.UserId,
                static (claim, activity) =>
                {
                    activity.SetTag("user.id", claim.Value);
                }
            },
            {
                AltinnCoreClaimTypes.PartyID,
                static (claim, activity) =>
                {
                    activity.SetTag("user.party.id", claim.Value);
                }
            },
            {
                AltinnCoreClaimTypes.AuthenticationLevel,
                static (claim, activity) =>
                {
                    activity.SetTag("user.authentication.level", claim.Value);
                }
            },
            {
                AltinnCoreClaimTypes.Org,
                static (claim, activity) =>
                {
                    activity.SetTag("user.application.owner.id", claim.Value);
                }
            },
            { 
                AltinnCoreClaimTypes.OrgNumber, 
                static (claim, activity) =>
                {
                    activity.SetTag("user.organization.number", claim.Value);
                }
            },
            {
                "authorization_details",
                static (claim, activity) =>
                {
                    SystemUserClaim? claimValue = JsonSerializer.Deserialize<SystemUserClaim>(claim.Value);
                    activity.SetTag("user.system.id", claimValue?.Systemuser_id[0] ?? null);
                    activity.SetTag("user.system.owner.number", claimValue?.Systemuser_org.ID ?? null);
                }
            },
        };

        return actions.ToFrozenDictionary();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryEnrichingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public TelemetryEnrichingMiddleware(RequestDelegate next, ILogger<TelemetryEnrichingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to process the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = context.Features.Get<IHttpActivityFeature>()?.Activity;
        if (activity is null)
        {
            await _next(context);
            return;
        }

        try
        {
            foreach (var claim in context.User.Claims)
            {
                if (_claimActions.TryGetValue(claim.Type, out var action))
                {
                    action(claim, activity);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while enriching telemetry.");
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding the <see cref="TelemetryEnrichingMiddleware"/> to the application pipeline.
/// </summary>
public static class TelemetryEnrichingMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="TelemetryEnrichingMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseTelemetryEnricher(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TelemetryEnrichingMiddleware>(
            app.ApplicationServices.GetRequiredService<ILogger<TelemetryEnrichingMiddleware>>());
    }
}
