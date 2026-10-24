namespace Altinn.Notifications.Tools.Simulator.Configuration;

/// <summary>
/// Settings for the simulator that holds requests the mocks do not recognise until an operator answers them.
/// </summary>
public class SimulatorSettings
{
    /// <summary>
    /// The localhost port the simulator listens on. The mocks' <c>FallbackUrl</c> must point here.
    /// </summary>
    public int Port { get; set; } = 5096;

    /// <summary>
    /// Whether the mocks forward what they do not recognise here. Only tells the GUI, since the mocks' <c>FallbackUrl</c> decides it.
    /// </summary>
    public bool Intercept { get; set; } = true;

    /// <summary>
    /// How long a request is held before it gets a 504 with <c>X-Simulator: timeout</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// The folder with response templates, one subfolder per system and <c>generic</c> for all. Relative to the output folder.
    /// </summary>
    public string TemplatesPath { get; set; } = "templates";

    /// <summary>
    /// How many handled requests the history keeps.
    /// </summary>
    public int HistorySize { get; set; } = 200;

    /// <summary>
    /// The Aspire dashboard, e.g. <c>https://localhost:17043</c>. When set, requests with a <c>traceparent</c> link to their trace.
    /// </summary>
    public string DashboardUrl { get; set; } = string.Empty;
}
