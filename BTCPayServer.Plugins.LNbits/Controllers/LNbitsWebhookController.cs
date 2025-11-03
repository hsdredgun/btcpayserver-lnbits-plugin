using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BTCPayServer.Data;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Events;

namespace BTCPayServer.Plugins.LNbits.Controllers
{
    [ApiController]
    [Route("plugins/lnbits")]
    [AllowAnonymous]
    public class LNbitsWebhookController : ControllerBase
    {
        private readonly ILogger<LNbitsWebhookController> _logger;
        private readonly ApplicationDbContextFactory _dbFactory;
        private readonly InvoiceRepository _invoiceRepo;
        private readonly EventAggregator _eventAggregator;

        public LNbitsWebhookController(
            ILogger<LNbitsWebhookController> logger,
            ApplicationDbContextFactory dbFactory,
            InvoiceRepository invoiceRepo,
            EventAggregator eventAggregator)
        {
            _logger = logger;
            _dbFactory = dbFactory;
            _invoiceRepo = invoiceRepo;
            _eventAggregator = eventAggregator;
        }

        private class Payload
        {
            public string payment_hash { get; set; }
            public bool paid { get; set; }
        }

        [HttpPost("webhook/{paymentHash}")]
        public async Task<IActionResult> Receive(string paymentHash)
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation("LNbits webhook received for payment hash: {PaymentHash}", paymentHash);

                var payload = JsonSerializer.Deserialize<Payload>(body, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (payload == null || !payload.paid)
                {
                    _logger.LogWarning("Invalid webhook payload");
                    return BadRequest("Invalid payload");
                }

                await using var ctx = _dbFactory.CreateContext();
                
                // Get all payments with Blob2 data
                // Blob2 is a STRING, not a byte array!
                var allPayments = await ctx.Payments
                    .Include(p => p.InvoiceData)
                    .Where(p => !string.IsNullOrEmpty(p.Blob2))
                    .ToListAsync();

                PaymentData foundPayment = null;
                
                // Search through payments to find one containing our payment hash
                foreach (var pmt in allPayments)
                {
                    if (string.IsNullOrEmpty(pmt.Blob2)) continue;
                    
                    try
                    {
                        // Blob2 is already a string! No conversion needed!
                        string blobText = pmt.Blob2;
                        
                        // Simple case-insensitive search
                        if (blobText.IndexOf(paymentHash, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundPayment = pmt;
                            _logger.LogInformation("Found matching payment");
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (foundPayment == null || foundPayment.InvoiceData == null)
                {
                    _logger.LogWarning("Payment not found for hash: {PaymentHash}", paymentHash);
                    return NotFound("Payment not found");
                }

                // Get the invoice ID
                string invoiceId = foundPayment.InvoiceData.Id;
                
                // Get the full invoice entity
                var invoiceEntity = await _invoiceRepo.GetInvoice(invoiceId);
                if (invoiceEntity == null)
                {
                    _logger.LogWarning("Invoice not found");
                    return NotFound("Invoice not found");
                }

                _logger.LogInformation("Found invoice {InvoiceId}", invoiceEntity.Id);

                // Trigger the payment received event
                // This will update the invoice status and show confetti! 🎉
                _eventAggregator.Publish(new InvoiceEvent(invoiceEntity, InvoiceEvent.ReceivedPayment));
                
                _logger.LogInformation("LNbits webhook: Invoice payment confirmed!");
                
                return Ok(new { success = true, invoiceId = invoiceEntity.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LNbits webhook");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}