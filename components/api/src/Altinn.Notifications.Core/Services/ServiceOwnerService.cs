using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IServiceOwnerService"/> that keeps the registered senders in memory.
/// </summary>
public class ServiceOwnerService(
    IServiceOwnerRepository repository,
    IOptions<NotificationConfig> config,
    ILogger<ServiceOwnerService> logger) : IServiceOwnerService
{
    private readonly NotificationConfig _config = config.Value;
    private readonly ILogger<ServiceOwnerService> _logger = logger;
    private readonly IServiceOwnerRepository _repository = repository;

    private volatile Dictionary<(SenderAddressType Type, string Owner), HashSet<string>>? _registeredSenders;

    /// <inheritdoc/>
    public async Task LoadSenders(CancellationToken cancellationToken = default)
    {
        var registeredSenders = await _repository.GetRegisteredSenders(cancellationToken);

        _registeredSenders = registeredSenders
            .GroupBy(sender => (sender.AddressType, sender.ServiceOwner.ToLowerInvariant()))
            .ToDictionary(group => group.Key, group => group.Select(sender => sender.SenderAddress).ToHashSet(StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    public ProblemInstance? ValidateSenders(string serviceOwner, IEnumerable<string?> emailSenders, IEnumerable<string?> smsSenders)
    {
        var invalidEmailSenders = FindInvalidSenders(serviceOwner, SenderAddressType.Email, _config.EnforceEmailSenderOwnership, _config.DefaultEmailFromAddress, emailSenders);
        if (invalidEmailSenders.Count > 0)
        {
            return CreateProblem(Problems.InvalidEmailSender, serviceOwner, "sender email addresses", invalidEmailSenders);
        }

        var invalidSmsSenders = FindInvalidSenders(serviceOwner, SenderAddressType.Sms, _config.EnforceSmsSenderOwnership, _config.DefaultSmsSenderNumber, smsSenders);
        if (invalidSmsSenders.Count > 0)
        {
            return CreateProblem(Problems.InvalidSmsSender, serviceOwner, "SMS senders", invalidSmsSenders);
        }

        return null;
    }

    private List<string> FindInvalidSenders(string serviceOwner, SenderAddressType addressType, bool enforce, string defaultSender, IEnumerable<string?> senders)
    {
        if (!enforce)
        {
            return [];
        }

        List<string> specified = [.. senders.OfType<string>().Select(s => s.Trim()).Distinct(StringComparer.Ordinal)];

        List<string> blank = [.. specified.Where(string.IsNullOrWhiteSpace)];
        List<string> toLookUp = [.. specified.Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, defaultSender, StringComparison.Ordinal))];

        if (toLookUp.Count == 0)
        {
            return blank;
        }

        return [.. blank, .. GetUnregisteredSenders(serviceOwner, addressType, toLookUp)];
    }

    private List<string> GetUnregisteredSenders(string serviceOwner, SenderAddressType addressType, List<string> senders)
    {
        var registered = _registeredSenders ?? throw new InvalidOperationException("The registered senders have not been loaded.");

        registered.TryGetValue((addressType, serviceOwner.ToLowerInvariant()), out var ownerSenders);

        return [.. senders.Where(s => ownerSenders == null || !ownerSenders.Contains(s))];
    }

    private ProblemInstance CreateProblem(ProblemDescriptor descriptor, string serviceOwner, string senderKind, List<string> invalidSenders)
    {
        string senders = string.Join(", ", invalidSenders.Select(s => $"'{s}'"));

        _logger.LogWarning("Rejected {Count} {SenderKind} not registered for service owner {ServiceOwner}", invalidSenders.Count, senderKind, serviceOwner);

        return ProblemInstance.Create(descriptor, $"The following {senderKind} are not registered for this service owner: {senders}.");
    }
}
