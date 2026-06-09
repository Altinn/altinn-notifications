using System.Diagnostics;

using Altinn.Notifications.Sms.Integrations.Configuration;
using Altinn.Notifications.Sms.Integrations.Wolverine.Policies;

namespace Altinn.Notifications.Sms.Tests.Sms.Integrations.Wolverine;

public class SendSmsCommandHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsUnreachableException()
    {
        var policy = new SendSmsCommandHandlerPolicy(new WolverineSettings());

        Assert.Throws<UnreachableException>(() => policy.Apply([], null!, null!));
    }
}
