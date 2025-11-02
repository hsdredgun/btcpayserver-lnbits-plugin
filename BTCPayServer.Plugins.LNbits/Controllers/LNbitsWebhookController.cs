using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbits.Controllers
{
    [ApiController]
    [Route("plugins/lnbits")]
    [AllowAnonymous] // Webhooks come from external LNbits instances
    public class LNbitsWebhookController : ControllerBase
    {
        private readonly InvoiceRepository _invoiceRepository;
        private readonly EventAggregator _eventAggregator;
        private readonly ILogger<LNbitsWebhookController> _logger;

        public LNbitsWebhookController(
            InvoiceRepository invoiceRepository,
            EventAggregator eventAggregator,
            ILogger<LNbitsWebhookController> logger)
        {
            _invoiceRepository = invoiceRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        [HttpPost("webhook/{paymentHash}")]
        public async Task<IActionResult> ReceiveWebhook(string paymentHash)
        {
            try
            {
                _logger.LogInformation("LNbits webhook received for payment hash: {PaymentHash}", paymentHash);

                // Try to find the invoice by searching through all invoices
                var allInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery());
                
                InvoiceEntity invoice = null;
                foreach (var inv in allInvoices)
                {
                    // Serialize the invoice to JSON and search for payment hash
                    var invoiceJson = JsonConvert.SerializeObject(inv);
                    if (invoiceJson.Contains(paymentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        invoice = inv;
                        break;
                    }
                }
                
                if (invoice == null)
                {
                    _logger.LogWarning("Invoice not found for payment hash: {PaymentHash}", paymentHash);
                    return NotFound("Invoice not found");
                }

                // Check if already paid
                var state = invoice.GetInvoiceState();
                if (state.Status == InvoiceStatus.Settled || state.Status == InvoiceStatus.Processing)
                {
                    _logger.LogInformation("Invoice {InvoiceId} already settled", invoice.Id);
                    return Ok("Already paid");
                }

                // Trigger invoice payment event (this triggers the confetti!)
                _logger.LogInformation("Triggering payment event for invoice {InvoiceId}", invoice.Id);
                _eventAggregator.Publish(new InvoiceEvent(invoice, InvoiceEvent.ReceivedPayment));

                return Ok("Webhook processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LNbits webhook for payment hash: {PaymentHash}", paymentHash);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}