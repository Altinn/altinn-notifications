using System.Diagnostics;

using Altinn.Notifications.Email.Core.Exceptions;
using Altinn.Notifications.Email.Integrations.Configuration;
using Altinn.Notifications.Email.Integrations.Wolverine.Policies;
using Altinn.Notifications.Shared.Commands;

using Wolverine;
using Wolverine.Runtime.Handlers;

using Xunit;

namespace Altinn.Notifications.Email.Tests.Email.Integrations.Wolverine;

public class SendComposedEmailCommandHandlerPolicyTests
{
    [Fact]
    public void Apply_WhenNoMatchingChain_ThrowsUnreachableException()
    {
        var policy = new SendComposedEmailCommandHandlerPolicy(new WolverineSettings());

        Assert.Throws<UnreachableException>(() => policy.Apply([], null!, null!));
    }

    [Fact]
    public void Apply_AttachmentDownloadException_IsRegisteredForRetry()
    {
        // Arrange
        var settings = new WolverineSettings
        {
            ComposedEmailSendQueuePolicy = new() { CooldownDelaysMs = [500], ScheduleDelaysMs = [5000] }
        };
        var graph = new HandlerGraph();
        var chain = new HandlerChain(typeof(SendComposedEmailCommand), graph);
        var policy = new SendComposedEmailCommandHandlerPolicy(settings);
        var envelope = new Envelope { Attempts = 1 };

        // Act
        policy.Apply([chain], null!, null!);

        // Assert
        var exception = new AttachmentDownloadException("file.pdf", new HttpRequestException("network error"));
        bool isHandled = chain.Failures.Any(rule => rule.TryCreateContinuation(exception, envelope, out _));
        Assert.True(isHandled);
    }
}
