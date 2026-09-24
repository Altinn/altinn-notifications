using System.Threading;

using Altinn.Notifications.Email.Core.Sending;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

internal sealed class DelegatingSendingService(ISendingService initialService) : ISendingService
{
    private ISendingService _currentService = initialService;

    public void SetCurrentService(ISendingService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        Interlocked.Exchange(ref _currentService, service);
    }

    public Task SendAsync(Core.Sending.Email email)
    {
        return Volatile.Read(ref _currentService).SendAsync(email);
    }

    public Task SendComposedAsync(ComposedEmail email, CancellationToken cancellationToken = default)
    {
        return Volatile.Read(ref _currentService).SendComposedAsync(email, cancellationToken);
    }
}
