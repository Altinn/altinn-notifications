using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Services;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class DisabledEmailCommandPublisherTests
{
    private readonly Email _email = new(Guid.NewGuid(), "Subject", "Body", "from@example.com", "to@example.com", EmailContentType.Plain);

    [Fact]
    public async Task PublishAsync_Always_ThrowsInvalidOperationException()
    {
        var publisher = new DisabledEmailCommandPublisher();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(_email, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ExceptionMessage_ContainsNotificationId()
    {
        var publisher = new DisabledEmailCommandPublisher();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(_email, CancellationToken.None));

        Assert.Contains(_email.NotificationId.ToString(), ex.Message);
    }

    [Fact]
    public async Task PublishAsync_ExceptionMessage_MentionsMisconfiguredFlags()
    {
        var publisher = new DisabledEmailCommandPublisher();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(_email, CancellationToken.None));

        Assert.Contains("EnableSendEmailPublisher", ex.Message);
        Assert.Contains("EnableWolverine", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_Batch_Always_ThrowsInvalidOperationException()
    {
        var publisher = new DisabledEmailCommandPublisher();
        var emails = new List<Email> { _email };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(emails, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_Batch_ExceptionMessage_ContainsBatchCount()
    {
        var publisher = new DisabledEmailCommandPublisher();
        var emails = new List<Email> { _email, _email };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(emails, CancellationToken.None));

        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_Batch_ExceptionMessage_MentionsMisconfiguredFlags()
    {
        var publisher = new DisabledEmailCommandPublisher();
        var emails = new List<Email> { _email };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(emails, CancellationToken.None));

        Assert.Contains("EnableSendEmailPublisher", ex.Message);
        Assert.Contains("EnableWolverine", ex.Message);
    }
}
