using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using BTCPayServer.Lightning;

namespace BTCPayServer.Lightning.LNbits
{
    public class LNbitsLightningClient : ILightningClient
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _baseUri;
        private readonly string _apiKey;
        private readonly string _walletId;
        private readonly string _btcpayServerUrl;
        
        public LNbitsLightningClient(Uri baseUri, string apiKey, string walletId = null, HttpClient httpClient = null, string btcpayServerUrl = null)
        {
            _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _walletId = walletId;
            _btcpayServerUrl = btcpayServerUrl;
            _httpClient = httpClient ?? new HttpClient();

            _httpClient.BaseAddress = _baseUri;
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry, CancellationToken cancellation = default)
            => CreateInvoice(new CreateInvoiceParams(amount, description, expiry), cancellation);

        public async Task<LightningInvoice> CreateInvoice(CreateInvoiceParams invoiceParams, CancellationToken cancellation = default)
        {
            var amountSats = (long)invoiceParams.Amount.ToUnit(LightMoneyUnit.Satoshi);
            var payload = new
            {
                @out = false,
                amount = amountSats,
                memo = invoiceParams.Description ?? "BTCPay Server Invoice",
                unit = "sat",
                expiry = (int)invoiceParams.Expiry.TotalSeconds
            };

            var resp = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_httpClient, "/api/v1/payments", payload, cancellation);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<LNbitsInvoiceResponse>(cancellationToken: cancellation);

            // Register webhook with LNbits
            await RegisterWebhookAsync(result.payment_hash, cancellation);

            return new LightningInvoice
            {
                Id = result.payment_hash,
                PaymentHash = result.payment_hash,
                BOLT11 = result.payment_request,
                Status = LightningInvoiceStatus.Unpaid,
                Amount = invoiceParams.Amount,
                ExpiresAt = DateTimeOffset.UtcNow.Add(invoiceParams.Expiry)
            };
        }

        public Task<LightningInvoice> GetInvoice(string invoiceId, CancellationToken cancellation = default)
            => GetInvoice(uint256.Parse(invoiceId), cancellation);

        public async Task<LightningInvoice> GetInvoice(uint256 paymentHash, CancellationToken cancellation = default)
        {
            var resp = await _httpClient.GetAsync($"/api/v1/payments/{paymentHash}", cancellation);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<LNbitsPaymentResponse>(cancellationToken: cancellation);

            var status = result.paid ? LightningInvoiceStatus.Paid : LightningInvoiceStatus.Unpaid;
            var amount = LightMoney.Satoshis(result.amount);

            return new LightningInvoice
            {
                Id = result.payment_hash,
                PaymentHash = result.payment_hash,
                BOLT11 = result.bolt11,
                Status = status,
                Amount = amount,
                PaidAt = result.paid && result.time > 0 ? DateTimeOffset.FromUnixTimeSeconds(result.time) : null
            };
        }

        public Task<PayResponse> Pay(string bolt11, PayInvoiceParams payParams = null, CancellationToken cancellation = default)
            => Pay(bolt11, cancellation);

        public async Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = default)
        {
            var payload = new { bolt11 };
            var resp = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_httpClient, "/api/v1/payments", payload, cancellation);
            return resp.IsSuccessStatusCode ? new PayResponse(PayResult.Ok) : new PayResponse(PayResult.Unknown);
        }

        public Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = default)
            => Task.FromResult(new PayResponse(PayResult.Unknown));

        public Task<LightningPayment> GetPayment(string paymentHash, CancellationToken cancellation = default)
            => Task.FromResult<LightningPayment>(null);

        public Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = default)
            => ListInvoices(new ListInvoicesParams(), cancellation);

        public Task<LightningInvoice[]> ListInvoices(ListInvoicesParams _, CancellationToken cancellation = default)
            => Task.FromResult(Array.Empty<LightningInvoice>());

        public Task<LightningPayment[]> ListPayments(CancellationToken cancellation = default)
            => ListPayments(new ListPaymentsParams(), cancellation);

        public Task<LightningPayment[]> ListPayments(ListPaymentsParams _, CancellationToken cancellation = default)
            => Task.FromResult(Array.Empty<LightningPayment>());

        public async Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = default)
        {
            try
            {
                var resp = await _httpClient.GetAsync("/api/v1/wallet", cancellation);
                resp.EnsureSuccessStatusCode();
                
                return new LightningNodeInformation
                {
                    BlockHeight = int.MaxValue,
                };
            }
            catch
            {
                return new LightningNodeInformation
                {
                    BlockHeight = 0,
                };
            }
        }

        public Task CancelInvoice(string invoiceId, CancellationToken cancellation = default)
            => Task.CompletedTask;

        public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = default)
            => Task.FromResult(Array.Empty<LightningChannel>());

        public Task<LightningChannel[]> ListAllChannels(CancellationToken cancellation = default)
            => Task.FromResult(Array.Empty<LightningChannel>());

        public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = default)
        {
            try
            {
                var resp = await _httpClient.GetAsync("/api/v1/wallet", cancellation);
                resp.EnsureSuccessStatusCode();
                var wallet = await resp.Content.ReadFromJsonAsync<LNbitsWalletInfo>(cancellationToken: cancellation);

                var onchain = new OnchainBalance
                {
                    Confirmed = Money.Zero,
                    Unconfirmed = Money.Zero
                };

                var offchain = new OffchainBalance
                {
                    Opening = LightMoney.Zero,
                    Local = LightMoney.Satoshis(wallet.balance),
                    Remote = LightMoney.Zero,
                    Closing = LightMoney.Zero
                };

                return new LightningNodeBalance(onchain, offchain);
            }
            catch
            {
                var onchain = new OnchainBalance
                {
                    Confirmed = Money.Zero,
                    Unconfirmed = Money.Zero
                };

                var offchain = new OffchainBalance
                {
                    Opening = LightMoney.Zero,
                    Local = LightMoney.Zero,
                    Remote = LightMoney.Zero,
                    Closing = LightMoney.Zero
                };

                return new LightningNodeBalance(onchain, offchain);
            }
        }

        public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = default)
            => throw new NotSupportedException("LNbits does not support on-chain deposit addresses.");

        public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = default)
            => Task.FromResult(ConnectionResult.CouldNotConnect);

        public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest request, CancellationToken cancellation = default)
            => throw new NotSupportedException("Channel management not supported.");

        public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = default)
            => Task.FromResult<ILightningInvoiceListener>(null);

        public void Dispose() => _httpClient?.Dispose();

        // Webhook registration method
        private async Task RegisterWebhookAsync(string paymentHash, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(_btcpayServerUrl))
            {
                // No BTCPay URL configured, skip webhook registration
                return;
            }

            try
            {
                var webhookUrl = $"{_btcpayServerUrl.TrimEnd('/')}/plugins/lnbits/webhook/{paymentHash}";
                
                var payload = new { webhook = webhookUrl };
                
                var response = await System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(_httpClient, $"/api/v1/payments/{paymentHash}", payload, cancellation);
                
                // LNbits returns 201 or 200 on success
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Webhook registered: {webhookUrl}");
                }
            }
            catch (Exception ex)
            {
                // Don't fail invoice creation if webhook registration fails
                System.Diagnostics.Debug.WriteLine($"Failed to register webhook: {ex.Message}");
            }
        }
    }

    internal class LNbitsInvoiceResponse
    {
        public string payment_hash { get; set; }
        public string payment_request { get; set; }
        public string checking_id { get; set; }
    }

    internal class LNbitsPaymentResponse
    {
        public string payment_hash { get; set; }
        public bool paid { get; set; }
        public long amount { get; set; }
        public string bolt11 { get; set; }
        public long time { get; set; }
        public string memo { get; set; }
    }

    internal class LNbitsWalletInfo
    {
        public long balance { get; set; }
    }
}