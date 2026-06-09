using System.Diagnostics;

using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Wolverine.Policies;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class CheckEmailSendStatusHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsUnreachableException()
    {
        var policy = new CheckEmailSendStatusHandlerPolicy(new WolverineSettings());

        Assert.Throws<UnreachableException>(() => policy.Apply([], null!, null!));
    }
}
