using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Adds the LinkMobility proxy, which stands in for the LinkMobility SMS gateway.
/// </summary>
public static class LinkMobilityMockBuilderExtensions
{
    // Test-only credentials. The mock sends its delivery reports with them, and the SMS service rejects reports that do not have them.
    private const string _deliveryReportUsername = "linkmobility-mock";
    private const string _deliveryReportPassword = "linkmobility-mock-test-password";

    /// <summary>
    /// Adds the LinkMobility proxy on <paramref name="port"/>, as a child of the test harness control panel. It logs the payloads
    /// masked and sends a copy of each SMS and its delivery state to the control panel's Phone tab.
    /// </summary>
    /// <remarks>
    /// Without <paramref name="target"/> it is a stub that answers by recipient number and posts delivery reports to the SMS service,
    /// so nothing leaves the machine. With <paramref name="interactive"/> every send is held in the control panel until an operator
    /// answers, which needs the control panel's intercept.
    /// </remarks>
    /// <param name="builder">The app host builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="port">The localhost port the proxy listens on.</param>
    /// <param name="testHarness">The control panel from <see cref="TestHarnessBuilderExtensions.AddTestHarness"/>.</param>
    /// <param name="target">A LinkMobility gateway to forward to, or empty to answer as a stub.</param>
    /// <param name="interactive">Whether every send is held in the control panel.</param>
    /// <param name="deliveryReportDelaySeconds">How long the stub waits before it posts a delivery report.</param>
    public static IResourceBuilder<ProjectResource> AddLinkMobilityMock(
        this IDistributedApplicationBuilder builder,
        string name,
        int port,
        IResourceBuilder<ProjectResource> testHarness,
        string target = "",
        bool interactive = false,
        int deliveryReportDelaySeconds = 2)
    {
        var harness = TestHarnessAnnotation.Of(testHarness);
        if (interactive && !harness.Intercept)
        {
            throw new InvalidOperationException("LinkMobility:Interactive needs TestHarness:Intercept.");
        }

        return builder.AddProject(name, MockProjects.LinkMobilityProxy, launchProfileName: null)
            .WithEnvironment("LinkMobilityProxySettings__Port", port.ToString())
            .WithEnvironment("LinkMobilityProxySettings__Target", target)
            .WithEnvironment("LinkMobilityProxySettings__Interactive", interactive.ToString())
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportUsername", _deliveryReportUsername)
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportPassword", _deliveryReportPassword)
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportDelaySeconds", deliveryReportDelaySeconds.ToString())
            .WithAnnotation(new LinkMobilityMockAnnotation(port, interactive ? harness.TimeoutSeconds : null))
            .WithTestHarness(testHarness, "LinkMobilityProxySettings", mirror: true);
    }

    /// <summary>
    /// Makes the SMS service send to <paramref name="linkMobility"/> instead of the gateway in its own settings, and the proxy post
    /// its delivery reports to the SMS service.
    /// </summary>
    /// <param name="sms">The SMS service.</param>
    /// <param name="linkMobility">The LinkMobility proxy.</param>
    public static IResourceBuilder<ProjectResource> WithLinkMobilityMock(this IResourceBuilder<ProjectResource> sms, IResourceBuilder<ProjectResource> linkMobility)
    {
        if (!linkMobility.Resource.TryGetLastAnnotation<LinkMobilityMockAnnotation>(out var mock))
        {
            throw new ArgumentException($"{linkMobility.Resource.Name} is not a LinkMobility proxy. Add it with AddLinkMobilityMock.", nameof(linkMobility));
        }

        linkMobility.WithEnvironment("LinkMobilityProxySettings__DeliveryReportUrl", ReferenceExpression.Create($"{sms.GetEndpoint("http")}/notifications/sms/api/v1/reports"));

        sms.WithEnvironment("SmsGatewaySettings__Endpoint", "http://localhost:" + mock.Port)
            .WithEnvironment("SmsDeliveryReportSettings__UserSettings__Username", _deliveryReportUsername)
            .WithEnvironment("SmsDeliveryReportSettings__UserSettings__Password", _deliveryReportPassword)
            .WaitFor(linkMobility);

        if (mock.HoldSeconds is int holdSeconds)
        {
            // The SMS service gives up on the gateway after 30 seconds by default, long before an operator has answered.
            sms.WithEnvironment("SmsGatewaySettings__TimeoutInSeconds", (holdSeconds + 10).ToString());
        }

        return sms;
    }

    /// <summary>
    /// The proxy's port, and with interactive on, how long the control panel holds each send.
    /// </summary>
    private sealed record LinkMobilityMockAnnotation(int Port, int? HoldSeconds) : IResourceAnnotation;
}
