using System;
using Microsoft.AspNetCore.Http;
using NBitcoin;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.LNbits;  // ADD THIS LINE - fixes the LNbitsLightningClient error

namespace BTCPayServer.Plugins.LNbits
{
    public class LNbitsConnectionStringHandler : ILightningConnectionStringHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LNbitsConnectionStringHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public ILightningClient Create(string connectionString, Network network, out string error)
        {
            if (!TryParseConnectionString(connectionString, out var baseUri, out var apiKey))
            {
                error = "Invalid format. Use: type=lnbits;server=https://your-lnbits.com;api-key=your-key";
                return null;
            }

            var request = _httpContextAccessor.HttpContext?.Request;
            var baseUrl = request != null ? $"{request.Scheme}://{request.Host}" : null;

            error = null;
            return new LNbitsLightningClient(baseUri, apiKey, null, null, baseUrl);
        }

        private bool TryParseConnectionString(string connectionString, out Uri baseUri, out string apiKey)
        {
            baseUri = null;
            apiKey = null;

            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            bool hasType = false;
            
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                var key = kv[0].Trim().ToLowerInvariant();
                var value = kv[1].Trim();

                if (key == "type" && value.ToLowerInvariant() == "lnbits")
                    hasType = true;
                else if (key == "server" && Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    baseUri = uri;
                else if (key == "api-key")
                    apiKey = value;
            }

            return hasType && baseUri != null && !string.IsNullOrEmpty(apiKey);
        }
    }
}