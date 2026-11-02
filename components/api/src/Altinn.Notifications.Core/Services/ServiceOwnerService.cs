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

    private volatile Dictionary<SenderAddressType, Dictionary<string, HashSet<string>>>? _registeredSenders;

    /// <inheritdoc/>
    public async Task LoadSenders(CancellationToken cancellationToken = default)
    {
        var registeredSenders = await _repository.GetRegisteredSenders(cancellationToken);

        _registeredSenders = registeredSenders
            .GroupBy(sender => sender.AddressType)
            .ToDictionary(
                typeGroup => typeGroup.Key,
                typeGroup => typeGroup
                    .GroupBy(sender => sender.ServiceOwner, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        ownerGroup => ownerGroup.Key,
                        ownerGroup => ownerGroup.Select(sender => sender.SenderAddress).ToHashSet(StringComparer.Ordinal),
                        StringComparer.OrdinalIgnoreCase));
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

        List<string> toLookUp = [.. senders
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .Where(s => !string.Equals(s, defaultSender, StringComparison.Ordinal))];

        if (toLookUp.Count == 0)
        {
            return [];
        }

        return GetUnregisteredSenders(serviceOwner, addressType, toLookUp);
    }

    private List<string> GetUnregisteredSenders(string serviceOwner, SenderAddressType addressType, List<string> senders)
    {
        var registered = _registeredSenders ?? throw new InvalidOperationException("The registered senders have not been loaded.");

        HashSet<string>? ownerSenders = null;
        if (registered.TryGetValue(addressType, out var owners))
        {
            owners.TryGetValue(serviceOwner, out ownerSenders);
        }

        return [.. senders.Where(s => ownerSenders == null || !ownerSenders.Contains(s))];
    }

    private ProblemInstance CreateProblem(ProblemDescriptor descriptor, string serviceOwner, string senderKind, List<string> invalidSenders)
    {
        string senders = string.Join(", ", invalidSenders);

        _logger.LogWarning("Rejected {SenderKind} not registered for service owner {ServiceOwner}: {Senders}", senderKind, serviceOwner, senders);

        return ProblemInstance.Create(descriptor, $"The following {senderKind} are not registered for this service owner: {senders}.");
    }
}
