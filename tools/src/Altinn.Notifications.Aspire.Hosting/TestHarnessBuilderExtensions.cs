using System.Net.Http.Json;

using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Adds the test harness control panel: its GUI, the trigger scheduler, and the commands that drive it from the dashboard.
/// </summary>
public static class TestHarnessBuilderExtensions
{
    /// <summary>
    /// The trigger jobs the control panel knows, the names in its <c>TriggerCatalog</c>.
    /// </summary>
    internal static readonly string[] TriggerJobs =
    [
        "sendemail",
        "sendemaildaytime",
        "sendsmsanytime",
        "sendsms",
        "terminateexpirednotifications",
        "deleteoldstatusfeedrecords",
        "pastdueoneorder",
        "retryoneorder",
    ];

    /// <summary>
    /// Adds the control panel on <paramref name="port"/>, with links to its tabs and commands to clear the Phone and Inbox tabs.
    /// </summary>
    /// <remarks>
    /// With <paramref name="intercept"/> the mocks added with it hand requests they do not recognise to it. It holds each one until an
    /// operator answers it in its GUI, or answers 504 after <paramref name="timeoutSeconds"/>. The dashboard shows a notification
    /// for each held request until it is answered.
    /// </remarks>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the control panel listens on.</param>
    /// <param name="intercept">Whether the mocks hand requests they do not recognise to the control panel.</param>
    /// <param name="timeoutSeconds">How long the control panel holds a request.</param>
    public static IResourceBuilder<ProjectResource> AddTestHarness(this IDistributedApplicationBuilder builder, string name, int port, bool intercept, int timeoutSeconds)
    {
        string url = "http://localhost:" + port;

        // The dashboard's own URL, so each held request can link to its trace.
        string dashboardUrl = (builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty).Split(';')[0];

        var harness = builder.AddProject(name, MockProjects.TestHarness, launchProfileName: null)
            .WithHttpEndpoint(port: port, targetPort: port, isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayText = "Control panel")
            .WithUrl(url + "/#triggers", "Triggers")
            .WithUrl(url + "/#phone", "Phone")
            .WithUrl(url + "/#inbox", "Inbox")
            .WithEnvironment("TestHarnessSettings__Port", port.ToString())
            .WithEnvironment("TestHarnessSettings__Intercept", intercept.ToString())
            .WithEnvironment("TestHarnessSettings__TimeoutSeconds", timeoutSeconds.ToString())
            .WithEnvironment("TestHarnessSettings__DashboardUrl", dashboardUrl)
            .WithAnnotation(new TestHarnessAnnotation(url, intercept, timeoutSeconds))
            .WithHttpCommand("/api/mirror/sms", "Clear Phone", commandName: "clear-phone", commandOptions: new HttpCommandOptions
            {
                Method = HttpMethod.Delete,
                Description = "Removes every SMS from the Phone tab.",
                IconName = "Delete",
            })
            .WithHttpCommand("/api/mirror/email", "Clear Inbox", commandName: "clear-inbox", commandOptions: new HttpCommandOptions
            {
                Method = HttpMethod.Delete,
                Description = "Removes every email from the Inbox tab.",
                IconName = "Delete",
            });

        if (intercept)
        {
            builder.Eventing.Subscribe<AfterResourcesCreatedEvent>((e, cancellationToken) =>
            {
                var client = e.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
                client.Timeout = Timeout.InfiniteTimeSpan;
                var notifier = new HeldRequestNotifier(
                    client,
                    new Uri(url),
                    e.Services.GetRequiredService<IInteractionService>(),
                    e.Services.GetRequiredService<ResourceLoggerService>().GetLogger(harness.Resource));

                _ = notifier.RunAsync(e.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
                return Task.CompletedTask;
            });
        }

        return harness;
    }

    /// <summary>
    /// Lets the control panel call the API's trigger endpoints on an interval, like the cron jobs in the cluster, and adds commands to
    /// run one now and to pause or resume them all.
    /// </summary>
    /// <param name="testHarness">The control panel.</param>
    /// <param name="api">The API.</param>
    /// <param name="enabled">Whether the jobs run on their own. Off, they start paused.</param>
    /// <param name="jobs">Overrides of the control panel's defaults, as <c>&lt;job&gt;:Enabled|IntervalSeconds|Hours</c>.</param>
    public static IResourceBuilder<ProjectResource> WithTriggers(this IResourceBuilder<ProjectResource> testHarness, IResourceBuilder<ProjectResource> api, bool enabled, IConfiguration jobs)
    {
        testHarness.WithEnvironment("TriggerSettings__Enabled", enabled.ToString())
            .WithEnvironment("TriggerSettings__ApiBaseUrl", api.GetEndpoint("http"));

        foreach (var job in jobs.GetChildren())
        {
            foreach (var setting in job.GetChildren())
            {
                testHarness.WithEnvironment($"TriggerSettings__Jobs__{job.Key}__{setting.Key}", setting.Value ?? string.Empty);
            }
        }

        // With intercept the mocks wait for the control panel and the API for the mocks, so the control panel cannot wait for the API.
        // Its first trigger calls then fail until the API is up.
        if (!TestHarnessAnnotation.Of(testHarness).Intercept)
        {
            testHarness.WaitFor(api);
        }

        return testHarness
            .WithHttpCommand("/api/triggers", "Run trigger", commandName: "run-trigger", commandOptions: new HttpCommandOptions
            {
                Description = "Calls one of the API's trigger endpoints now, whether its job is paused or not.",
                IconName = "Play",
                IsHighlighted = true,
                Arguments =
                [
                    new InteractionInput
                    {
                        Name = "job",
                        Label = "Trigger",
                        InputType = InputType.Choice,
                        Required = true,
                        Options = [.. TriggerJobs.Select(job => KeyValuePair.Create(job, job))],
                        Value = TriggerJobs[0],
                    },
                ],
                PrepareRequest = context =>
                {
                    string job = context.Arguments.GetString("job") ?? TriggerJobs[0];
                    context.Request.RequestUri = new Uri(context.Request.RequestUri!, $"/api/triggers/{Uri.EscapeDataString(job)}/run");
                    return Task.CompletedTask;
                },
                ResultMode = HttpCommandResultMode.Json,
            })
            .WithHttpCommand("/api/triggers", "Pause triggers", commandName: "pause-triggers", commandOptions: SetTriggers(false))
            .WithHttpCommand("/api/triggers", "Resume triggers", commandName: "resume-triggers", commandOptions: SetTriggers(true));
    }

    /// <summary>
    /// Adds commands that send an instant SMS or email order to the API, with a token from the mock token issuer. The message goes
    /// through the whole stack and shows up in the Phone or Inbox tab.
    /// </summary>
    /// <param name="testHarness">The control panel.</param>
    /// <param name="api">The API.</param>
    /// <param name="auth">The mock token issuer from <see cref="MockAuthBuilderExtensions.AddMockAuth"/>.</param>
    public static IResourceBuilder<ProjectResource> WithTestMessageCommands(this IResourceBuilder<ProjectResource> testHarness, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth)
    {
        var sender = new TestMessageSender(api.GetEndpoint("http"), auth.GetEndpoint("http"));

        return testHarness
            .WithCommand(
                "send-test-sms",
                "Send test SMS",
                context => sender.SendSmsAsync(context.Services.GetRequiredService<IHttpClientFactory>().CreateClient(), context.Arguments.GetString("recipient")!, context.Arguments.GetString("text")!, context.CancellationToken),
                new CommandOptions
                {
                    Description = "Orders an instant SMS from the API. It shows up in the Phone tab.",
                    IconName = "Phone",
                    Arguments =
                    [
                        new InteractionInput { Name = "recipient", Label = "Phone number", InputType = InputType.Text, Required = true, Value = "+4799999999", Description = "The last two digits pick the outcome, e.g. 01 ends as undelivered." },
                        new InteractionInput { Name = "text", Label = "Text", InputType = InputType.Text, Required = true, Value = "Test SMS from the Aspire dashboard" },
                    ],
                })
            .WithCommand(
                "send-test-email",
                "Send test email",
                context => sender.SendEmailAsync(context.Services.GetRequiredService<IHttpClientFactory>().CreateClient(), context.Arguments.GetString("recipient")!, context.Arguments.GetString("subject")!, context.Arguments.GetString("body")!, context.CancellationToken),
                new CommandOptions
                {
                    Description = "Orders an instant email from the API. It shows up in the Inbox tab.",
                    IconName = "Mail",
                    Arguments =
                    [
                        new InteractionInput { Name = "recipient", Label = "Email address", InputType = InputType.Text, Required = true, Value = "aspire@example.com", Description = "The local part picks the outcome, e.g. bounce@example.com bounces." },
                        new InteractionInput { Name = "subject", Label = "Subject", InputType = InputType.Text, Required = true, Value = "Test email from the Aspire dashboard" },
                        new InteractionInput { Name = "body", Label = "Body", InputType = InputType.Text, Required = true, Value = "Sent with the Send test email command." },
                    ],
                });
    }

    /// <summary>
    /// Shows the settings that decide what the app host started, e.g. <c>Auth:Mode</c>, as properties of the resource in the dashboard.
    /// </summary>
    /// <param name="builder">The resource.</param>
    /// <param name="settings">The settings and their values.</param>
    public static IResourceBuilder<T> WithActiveSettings<T>(this IResourceBuilder<T> builder, IReadOnlyDictionary<string, string> settings)
        where T : IResource
    {
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(builder.Resource, async (e, _) =>
        {
            var properties = settings.Select(s => new ResourcePropertySnapshot("setting." + s.Key, s.Value) { DisplayName = s.Key, IsHighlighted = true });
            await e.Services.GetRequiredService<ResourceNotificationService>().PublishUpdateAsync(builder.Resource, snapshot => snapshot with
            {
                Properties = [.. snapshot.Properties.Where(p => !p.Name.StartsWith("setting.", StringComparison.Ordinal)), .. properties],
            });
        });

        return builder;
    }

    private static HttpCommandOptions SetTriggers(bool enabled) => new()
    {
        Method = HttpMethod.Put,
        Description = enabled ? "Lets every trigger job run on its interval again." : "Stops every trigger job from running on its own. Run trigger still works.",
        IconName = enabled ? "PlayCircle" : "PauseCircle",
        PrepareRequest = context =>
        {
            context.Request.Content = JsonContent.Create(new { enabled });
            return Task.CompletedTask;
        },
    };
}
