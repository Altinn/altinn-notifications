using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

internal sealed class UnconfiguredEmailServiceClient : IEmailServiceClient
{
    private static InvalidOperationException CreateException(string methodName) =>
        new InvalidOperationException($"{nameof(IEmailServiceClient)}.{methodName} was called without a test-installed client. Call fixture reset/install helpers in test arrange.");

    public Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        throw CreateException(nameof(SendEmail));
    }

    public Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(
        Core.Sending.ComposedEmail email,
        CancellationToken cancellationToken = default)
    {
        throw CreateException(nameof(SendComposedEmail));
    }

    public Task<EmailSendResult> GetOperationUpdate(string operationId)
    {
        throw CreateException(nameof(GetOperationUpdate));
    }
}
