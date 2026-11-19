using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;

namespace Aspire.Hosting;

/// <summary>
/// Adds the LinkMobility proxy, which stands in for the LinkMobility SMS gateway.
/// </summary>
public static class LinkMobilityMockBuilderExtensions
{
    // The proxy sends its delivery reports with this user and a password generated for the checkout, and the SMS service rejects
    // reports that do not have them.
    private const string _deliveryReportUsername = "linkmobility-mock";

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
    public static IResourceBuilder<DotnetProjectResource> AddLinkMobilityMock(
        this IDistributedApplicationBuilder builder,
        string name,
        int port,
        IResourceBuilder<DotnetProjectResource> testHarness,
        string target = "",
        bool interactive = false,
        int deliveryReportDelaySeconds = 2)
    {
        var harness = TestHarnessAnnotation.Of(testHarness);
        if (interactive && !harness.Intercept)
        {
            throw new InvalidOperationException("LinkMobility:Interactive needs TestHarness:Intercept.");
        }

        var password = builder.AddGeneratedSecret($"{name}-delivery-report-password", 32);
        var linkMobility = builder.AddDotnetProject(name, MockProjects.LinkMobilityProxy, options => options.ExcludeLaunchProfile = true)
            .WithEnvironment("LinkMobilityProxySettings__Port", port.ToString())
            .WithEnvironment("LinkMobilityProxySettings__Target", target)
            .WithEnvironment("LinkMobilityProxySettings__Interactive", interactive.ToString())
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportUsername", _deliveryReportUsername)
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportPassword", password)
            .WithEnvironment("LinkMobilityProxySettings__DeliveryReportDelaySeconds", deliveryReportDelaySeconds.ToString())
            .WithAnnotation(new LinkMobilityMockAnnotation(port, interactive ? harness.TimeoutSeconds : null, password.Resource))
            .WithTestHarness(testHarness, "LinkMobilityProxySettings", mirror: true);

        password.WithParentRelationship(linkMobility);
        return linkMobility;
    }

    /// <summary>
    /// Makes the SMS service send to <paramref name="linkMobility"/> instead of the gateway in its own settings, and the proxy post
    /// its delivery reports to the SMS service.
    /// </summary>
    /// <param name="sms">The SMS service.</param>
    /// <param name="linkMobility">The LinkMobility proxy.</param>
    public static IResourceBuilder<DotnetProjectResource> WithLinkMobilityMock(this IResourceBuilder<DotnetProjectResource> sms, IResourceBuilder<DotnetProjectResource> linkMobility)
    {
        if (!linkMobility.Resource.TryGetLastAnnotation<LinkMobilityMockAnnotation>(out var mock))
        {
            throw new ArgumentException($"{linkMobility.Resource.Name} is not a LinkMobility proxy. Add it with AddLinkMobilityMock.", nameof(linkMobility));
        }

        linkMobility.WithEnvironment("LinkMobilityProxySettings__DeliveryReportUrl", ReferenceExpression.Create($"{sms.GetEndpoint("http")}/notifications/sms/api/v1/reports"));

        sms.WithEnvironment("SmsGatewaySettings__Endpoint", Localhost.Url(mock.Port))
            .WithEnvironment("SmsDeliveryReportSettings__UserSettings__Username", _deliveryReportUsername)
            .WithEnvironment("SmsDeliveryReportSettings__UserSettings__Password", mock.DeliveryReportPassword)
            .WaitFor(linkMobility);

        if (mock.HoldSeconds is int holdSeconds)
        {
            // The SMS service gives up on the gateway after 30 seconds by default, long before an operator has answered.
            sms.WithEnvironment("SmsGatewaySettings__TimeoutInSeconds", (holdSeconds + 10).ToString());
        }

        return sms;
    }

    /// <summary>
    /// The proxy's port, with interactive on how long the control panel holds each send, and the password on its delivery reports.
    /// </summary>
    private sealed record LinkMobilityMockAnnotation(int Port, int? HoldSeconds, ParameterResource DeliveryReportPassword) : IResourceAnnotation;
}
