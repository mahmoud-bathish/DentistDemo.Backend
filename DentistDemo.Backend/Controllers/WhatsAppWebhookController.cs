using DentistDemo.Backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DentistDemo.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly IOpenAIService _openAIService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ILogger<WhatsAppWebhookController> _logger;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _userThreads = new Dictionary<string, string>();

        public WhatsAppWebhookController(
            IOpenAIService openAIService,
            IWhatsAppService whatsAppService,
            ILogger<WhatsAppWebhookController> logger,
            IConfiguration configuration)
        {
            _openAIService = openAIService;
            _whatsAppService = whatsAppService;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery] string hub_mode, [FromQuery] string hub_verify_token, [FromQuery] string hub_challenge)
        {
            // Verify webhook for Meta WhatsApp Business API
            var verifyToken = _configuration["WhatsApp:VerifyToken"] ?? "your_verify_token_here";

            if (hub_mode == "subscribe" && hub_verify_token == verifyToken)
            {
                _logger.LogInformation("Webhook verified successfully");
                return Ok(hub_challenge);
            }

            _logger.LogWarning("Webhook verification failed");
            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage()
        {
            try
            {
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                _logger.LogInformation($"Received WhatsApp webhook: {requestBody}");

                var webhookData = JsonSerializer.Deserialize<MetaWebhookData>(requestBody);

                if (webhookData?.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value?.Messages?.FirstOrDefault() is var message)
                {
                    if (message != null && message.Type == "text")
                    {
                        var from = message.From;
                        var messageText = message.Text?.Body ?? "";
                        var messageId = message.Id;

                        _logger.LogInformation($"Processing message from {from}: {messageText}");

                        // Get or create thread for this user
                        if (!_userThreads.ContainsKey(from))
                        {
                            var thread = await _openAIService.CreateThread();
                            _userThreads[from] = thread.Id;
                            _logger.LogInformation($"Created new thread {thread.Id} for user {from}");
                        }

                        var threadId = _userThreads[from];

                        // Process the message with OpenAI Assistant
                        var gptResponse = await _openAIService.SendMessageToAssistantAsync(messageText, threadId);

                        // Send response back to WhatsApp
                        var sent = await _whatsAppService.SendMessageAsync(from, gptResponse.Message, messageId);

                        if (sent)
                        {
                            _logger.LogInformation($"Response sent successfully to {from}");
                        }
                        else
                        {
                            _logger.LogError($"Failed to send response to {from}");
                        }

                        return Ok(new { success = true, message = "Message processed successfully" });
                    }
                }

                return Ok(new { success = true, message = "Webhook received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WhatsApp webhook");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    // Meta WhatsApp Webhook Data Models
    public class MetaWebhookData
    {
        public string? Object { get; set; }
        public List<WebhookEntry>? Entry { get; set; }
    }

    public class WebhookEntry
    {
        public string? Id { get; set; }
        public List<WebhookChange>? Changes { get; set; }
    }

    public class WebhookChange
    {
        public string? Field { get; set; }
        public WebhookValue? Value { get; set; }
    }

    public class WebhookValue
    {
        public string? MessagingProduct { get; set; }
        public string? Metadata { get; set; }
        public List<WhatsAppMessage>? Messages { get; set; }
    }

    public class WhatsAppMessage
    {
        public string? From { get; set; }
        public string? Id { get; set; }
        public string? Timestamp { get; set; }
        public string? Type { get; set; }
        public MessageText? Text { get; set; }
    }

    public class MessageText
    {
        public string? Body { get; set; }
    }
}
