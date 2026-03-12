using Altinn.Notifications.Shared.Configuration;

namespace Altinn.Notifications.Sms.Integrations.Configuration;

/// <summary>
/// Wolverine/Azure Service Bus settings scoped to the SMS service.
/// Extends the shared base with queue names consumed by the SMS service.
/// </summary>
public class WolverineSettings : WolverineSettingsBase
{
}
