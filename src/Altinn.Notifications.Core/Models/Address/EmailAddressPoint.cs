﻿using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Address;

/// <summary>
/// A class represeting an address point
/// </summary>
public class EmailAddressPoint : IAddressPoint
{
    /// <inheritdoc/>
    public AddressType AddressType { get; private set; }

    /// <summary>
    /// Gets the email address
    /// </summary>
    public string EmailAddress { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressPoint"/> class.
    /// </summary>
    public EmailAddressPoint(string emailAddress)
    {
        AddressType = AddressType.Email; 
        EmailAddress = emailAddress;
    }
}