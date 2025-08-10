using DentistDemo.Backend.Interfaces;
using System.Text;
using System.Text.Json;

namespace DentistDemo.Backend.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _accessToken;
        private readonly string _phoneNumberId;
        private readonly string _apiUrl;

        public WhatsAppService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _accessToken = _configuration["WhatsApp:AccessToken"] ?? throw new InvalidOperationException("WhatsApp Access Token not configured");
            _phoneNumberId = _configuration["WhatsApp:PhoneNumberId"] ?? throw new InvalidOperationException("WhatsApp Phone Number ID not configured");
            _apiUrl = $"https://graph.facebook.com/v18.0/{_phoneNumberId}/messages";

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
        }

        public async Task<bool> SendMessageAsync(string to, string message)
        {
            return await SendMessageAsync(to, message, null);
        }

        public async Task<bool> SendMessageAsync(string to, string message, string? messageId)
        {
            try
            {
                var requestBody = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = to,
                    type = "text",
                    text = new
                    {
                        preview_url = false,
                        body = message
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"WhatsApp message sent successfully: {responseContent}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"WhatsApp API error: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending WhatsApp message: {ex.Message}");
                return false;
            }
        }
    }
}
