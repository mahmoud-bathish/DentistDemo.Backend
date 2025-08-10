using DentistDemo.Backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentistDemo.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IOpenAIService _openAIService;
        private readonly ILogger<TestController> _logger;

        public TestController(IOpenAIService openAIService, ILogger<TestController> logger)
        {
            _openAIService = openAIService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new OpenAI Assistant thread
        /// </summary>
        /// <returns>Thread information including the thread ID</returns>
        [HttpPost("create-thread")]
        public async Task<IActionResult> CreateThread()
        {
            try
            {
                _logger.LogInformation("Creating new OpenAI Assistant thread");

                var thread = await _openAIService.CreateThread();

                _logger.LogInformation($"Successfully created thread with ID: {thread.Id}");

                return Ok(new
                {
                    success = true,
                    message = "Thread created successfully",
                    thread = new
                    {
                        id = thread.Id,
                        object_type = thread.Object,
                        createdAt = thread.CreatedAt,
                        metadata = thread.Metadata
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating OpenAI Assistant thread");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to create thread",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Sends a message to the OpenAI Assistant and gets a response
        /// </summary>
        /// <param name="request">The message request containing the message text and thread ID</param>
        /// <returns>Assistant's response</returns>
        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Message cannot be empty"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.ThreadId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "ThreadId cannot be empty"
                    });
                }

                _logger.LogInformation($"Sending message to thread {request.ThreadId}: {request.Message}");

                var response = await _openAIService.SendMessageToAssistantAsync(request.Message, request.ThreadId);

                _logger.LogInformation($"Received response from assistant for thread {request.ThreadId}");

                return Ok(new
                {
                    success = true,
                    message = "Message sent successfully",
                    threadId = request.ThreadId,
                    response = new
                    {
                        message = response.Message
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending message to thread {request.ThreadId}");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to send message",
                    details = ex.Message,
                    threadId = request.ThreadId
                });
            }
        }

        /// <summary>
        /// Test endpoint to check if the controller is working
        /// </summary>
        /// <returns>Status information</returns>

    }

    /// <summary>
    /// Request model for sending messages to the assistant
    /// </summary>
    public class SendMessageRequest
    {
        /// <summary>
        /// The message text to send to the assistant
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The thread ID to send the message to
        /// </summary>
        public string ThreadId { get; set; } = string.Empty;
    }
}
