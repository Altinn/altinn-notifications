using System.Threading;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

internal sealed class DelegatingEmailServiceClient(IEmailServiceClient initialClient) : IEmailServiceClient
{
    private IEmailServiceClient _currentClient = initialClient;

    public void SetCurrentClient(IEmailServiceClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Interlocked.Exchange(ref _currentClient, client);
    }

    public Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        return Volatile.Read(ref _currentClient).SendEmail(email);
    }

    public Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(
        Core.Sending.ComposedEmail email,
        CancellationToken cancellationToken = default)
    {
        return Volatile.Read(ref _currentClient).SendComposedEmail(email, cancellationToken);
    }

    public Task<EmailSendResult> GetOperationUpdate(string operationId)
    {
        return Volatile.Read(ref _currentClient).GetOperationUpdate(operationId);
    }
}
