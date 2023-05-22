﻿#nullable disable
namespace Altinn.Notifications.Configuration;

/// <summary>
/// The key vault settings used to fetch certificate information from key vault
/// </summary>
public class KeyVaultSettings
{
    /// <summary>
    /// The key vault reader client id
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// The key vault client secret
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// The key vault tenant Id
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// The uri to the key vault
    /// </summary>
    public string SecretUri { get; set; }
}