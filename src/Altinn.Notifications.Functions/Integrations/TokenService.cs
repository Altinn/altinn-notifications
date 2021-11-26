using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Functions.Configurations;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Functions.Integrations
{
    public class TokenService : IToken
    {

        private readonly IKeyVaultService _keyVault;
        private readonly IAccessTokenGenerator _accessTokenGenerator;

        private readonly KeyVaultSettings _settings;

        public TokenService(IKeyVaultService keyVault, IAccessTokenGenerator accessTokenGenerator, IOptions<KeyVaultSettings> settings)
        {
            _keyVault = keyVault;
            _accessTokenGenerator = accessTokenGenerator;
            _settings = settings.Value;
        }

        public async Task<string> GeneratePlatformToken()
        {
            string certBase64 = await _keyVault.GetCertificateAsync(_settings.KeyVaultURI, _settings.PlatformCertSecretId);
           
            
            string accessToken = _accessTokenGenerator.GenerateAccessToken(
                "platform",
                "notifications",
                new X509Certificate2(Convert.FromBase64String(certBase64), (string)null, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable));
            return accessToken;
        }
    }
}
