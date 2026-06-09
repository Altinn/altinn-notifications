using System.Diagnostics;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Wolverine.Policies;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.Wolverine;

public class SmsDeliveryReportHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsUnreachableException()
    {
        var policy = new SmsDeliveryReportHandlerPolicy(new WolverineSettings());

        Assert.Throws<UnreachableException>(() => policy.Apply([], null!, null!));
    }
}
