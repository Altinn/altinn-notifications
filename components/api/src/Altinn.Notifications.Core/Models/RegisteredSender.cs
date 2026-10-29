using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// A sender address that a service owner is allowed to use when ordering notifications.
/// </summary>
/// <param name="ServiceOwner">The short name of the service owner, as used for the creator of an order.</param>
/// <param name="AddressType">The channel the sender address is registered for.</param>
/// <param name="SenderAddress">The sender email address or SMS sender, stored exactly as it is specified in order requests.</param>
public record RegisteredSender(string ServiceOwner, SenderAddressType AddressType, string SenderAddress);
