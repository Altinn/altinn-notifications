namespace Altinn.Notifications.Tools.MockAuth.Configuration;

/// <summary>
/// Settings for the local mock token issuer.
/// </summary>
public class MockAuthSettings
{
    /// <summary>
    /// The localhost port the issuer listens on.
    /// </summary>
    public int Port { get; set; } = 5094;

    /// <summary>
    /// Basic auth username for the token endpoints, like the Altinn test token generator. Required.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic auth password for the token endpoints. Required. The Aspire app host generates one for the checkout.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The RSA key to sign tokens with, as a base64-encoded PKCS#8 private key. Empty makes a new key each time the issuer starts.
    /// The Aspire app host makes one per run, so tokens stay valid when only the issuer restarts.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}
