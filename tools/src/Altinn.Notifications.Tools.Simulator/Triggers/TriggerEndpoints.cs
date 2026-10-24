using Altinn.Notifications.Tools.Simulator.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.Simulator.Triggers;

/// <summary>
/// <c>/api/triggers</c>, the API the GUI's Triggers tab uses to pause, resume and run the jobs.
/// </summary>
internal static class TriggerEndpoints
{
    public static void MapTriggerEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/triggers");
        api.MapGet(string.Empty, GetTriggers);
        api.MapPut(string.Empty, UpdateAll);
        api.MapPut("/{name}", Update);
        api.MapPost("/{name}/run", RunAsync);
    }

    private static IResult GetTriggers(TriggerScheduler scheduler, IOptions<TriggerSettings> options, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return Results.Ok(new
        {
            options.Value.ApiBaseUrl,
            Jobs = scheduler.Jobs.Select(job => View(job, now)),
        });
    }

    /// <summary>
    /// Pauses or resumes every job at once.
    /// </summary>
    private static IResult UpdateAll(TriggerUpdate update, TriggerScheduler scheduler)
    {
        if (update.Enabled is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["enabled"] = ["Set enabled to pause or resume all jobs."] });
        }

        foreach (var job in scheduler.Jobs)
        {
            scheduler.Update(job, update.Enabled, null, null);
        }

        return Results.NoContent();
    }

    private static IResult Update(string name, TriggerUpdate update, TriggerScheduler scheduler, TimeProvider timeProvider)
    {
        if (scheduler.Find(name) is not { } job)
        {
            return NotFound(name);
        }

        Dictionary<string, string[]> errors = [];
        if (update.IntervalSeconds is < 1 or > 86400)
        {
            errors["intervalSeconds"] = ["The interval must be between 1 and 86400 seconds."];
        }

        bool[]? hours = null;
        if (update.Hours is not null && !HourRange.TryParse(update.Hours, out hours))
        {
            errors["hours"] = ["Hours must be like the hour field of a cron expression, e.g. 7-16, or empty for all day."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        scheduler.Update(job, update.Enabled, update.IntervalSeconds, hours);
        return Results.Ok(View(job, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Runs the job once, now, whether it is paused or outside its hours, and answers with the run.
    /// </summary>
    private static async Task<IResult> RunAsync(string name, TriggerScheduler scheduler, CancellationToken cancellationToken)
    {
        if (scheduler.Find(name) is not { } job)
        {
            return NotFound(name);
        }

        var run = await scheduler.RunAsync(job, "manual", cancellationToken);
        return run is null
            ? Results.Conflict(new { error = $"{name} is already running." })
            : Results.Ok(run);
    }

    private static IResult NotFound(string name) =>
        Results.NotFound(new { error = $"No trigger {name}. Known: {string.Join(", ", TriggerCatalog.All.Select(d => d.Name))}." });

    private static object View(TriggerJob job, DateTimeOffset now) => new
    {
        job.Name,
        Path = "/" + TriggerCatalog.TriggerPath + job.Name,
        job.Definition.Description,
        job.Definition.ClusterSchedule,
        job.Enabled,
        job.IntervalSeconds,
        Hours = HourRange.Format(job.Hours),
        OutsideHours = !job.RunsAt(now),
        Running = job.Running.CurrentCount == 0,
        job.NextRunAt,
        LastRun = job.History.FirstOrDefault(),
        job.History,
    };

    internal sealed record TriggerUpdate(bool? Enabled, int? IntervalSeconds, string? Hours);
}
