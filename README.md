# LNbits Plugin for BTCPay Server

A BTCPay Server plugin that enables LNbits as a Lightning Network provider.

## Status: Working & Tested

Successfully tested on BTCPay Server v2.0 with invoice creation and Lightning payments.

##  Features

- ⚡ Connect BTCPay Server to LNbits
- 💰 Create Lightning invoices
- 🔑 Support for wallet-specific API keys
- 🌐 Works with external Bitcoin nodes (no sync required)
- 📦 Standalone plugin (not in core codebase)

##  Installation

1. Download the latest `.btcpay` file from releases
2. Go to BTCPay Server → Server Settings → Plugins
3. Click "Upload Plugin"
4. Upload the `.btcpay` file
5. Restart BTCPay Server

##  Configuration

### Connection String Format
```
type=lnbits;server=https://your-lnbits-url;wallet-id=optional-wallet-id;api-key=your-api-key
```

### Example
```
type=lnbits;server=https://legend.lnbits.com;wallet-id=abc123;api-key=xyz789
```

**Note:** `wallet-id` is optional. If not provided, LNbits will use the default wallet.

### Where to Find Your Credentials

1. Log into your LNbits instance
2. Go to your wallet
3. Click on "API Info" or "API Keys"
4. Copy your **Admin Key** or **Invoice/Read Key**
5. (Optional) Copy your **Wallet ID** from the URL

##  Usage

1. Install the plugin
2. Go to Store Settings → Lightning
3. Click "Use custom node"
4. Enter your LNbits connection string
5. Click "Test Connection"
6. Save!

##  Contributing

Contributions welcome! Please open an issue or PR.

## License

MIT License

##  Support

- BTCPay Server: https://btcpayserver.org
- LNbits: https://lnbits.com

---
*** Made with Love ***