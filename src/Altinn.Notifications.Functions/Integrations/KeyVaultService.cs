using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Functions.Integrations
{
    /// <summary>
    /// Wrapper implementation for a KeyVaultClient. The wrapped client is created with a principal obtained through configuration.
    /// </summary>
    /// <remarks>This class is excluded from code coverage because it has no logic to be tested.</remarks>
    [ExcludeFromCodeCoverage]
    public class KeyVaultService : IKeyVaultService
    {
        private readonly ILogger<IKeyVaultService> _logger;

        public KeyVaultService(ILogger<IKeyVaultService> logger)
        {
            _logger = logger;
                
        }
        /// <inheritdoc/>
        public async Task<string> GetCertificateAsync(string vaultUri, string secretId)
        {
            try
            {
                CertificateClient certificateClient = new CertificateClient(new Uri(vaultUri), new DefaultAzureCredential());
                AsyncPageable<CertificateProperties> certificatePropertiesPage = certificateClient.GetPropertiesOfCertificateVersionsAsync(secretId);
                await foreach (CertificateProperties certificateProperties in certificatePropertiesPage)
                {
                    if (certificateProperties.Enabled == true &&
                        (certificateProperties.ExpiresOn == null || certificateProperties.ExpiresOn >= DateTime.UtcNow))
                    {
                        SecretClient secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());

                        KeyVaultSecret secret = await secretClient.GetSecretAsync(certificateProperties.Name, certificateProperties.Version);
                        _logger.LogInformation($"// cert length {secret.Value.Length}");
                        return secret.Value;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
           

            return null;
        }
    }
}
