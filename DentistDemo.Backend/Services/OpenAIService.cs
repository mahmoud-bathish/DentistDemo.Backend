using DentistDemo.Backend.Interfaces;
using DentistDemo.Backend.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DentistDemo.Backend.Services
{
    public class OpenAIService : IOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IBookingService _bookingService;
        private readonly string _apiKey;

        public OpenAIService(HttpClient httpClient, IConfiguration configuration, IBookingService bookingService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _bookingService = bookingService;
            _apiKey = _configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<ThreadResponse> CreateThread()
        {
            var assistantId = _configuration["OpenAI:AssistantId"] ?? throw new InvalidOperationException("OpenAI AssistantId not configured");

            // Create thread
            using var createThreadRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/threads");
            createThreadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            createThreadRequest.Headers.Add("OpenAI-Beta", "assistants=v2");
            createThreadRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var threadResponse = await _httpClient.SendAsync(createThreadRequest);
            var content = await threadResponse.Content.ReadAsStringAsync();

            if (!threadResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to create thread. StatusCode: {threadResponse.StatusCode}, Response: {content}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var threadData = JsonSerializer.Deserialize<ThreadResponse>(content, options);
            return threadData ?? throw new InvalidOperationException("Failed to deserialize thread response");
        }

        public async Task<OpenAIResponse> SendMessageToAssistantAsync(string message, string threadId)
        {
            try
            {
                var assistantId = _configuration["OpenAI:AssistantId"] ?? throw new InvalidOperationException("OpenAI AssistantId not configured");

                // Add message to thread
                var messageRequest = new
                {
                    role = "user",
                    content = message
                };
                var messageContent = JsonSerializer.Serialize(messageRequest);

                using var messageRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://api.openai.com/v1/threads/{threadId}/messages");
                messageRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                messageRequestMessage.Headers.Add("OpenAI-Beta", "assistants=v2");
                messageRequestMessage.Content = new StringContent(messageContent, Encoding.UTF8, "application/json");

                var messageResponse = await _httpClient.SendAsync(messageRequestMessage);
                var messageResponseContent = await messageResponse.Content.ReadAsStringAsync();

                if (!messageResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to add message: {messageResponse.StatusCode}, Body: {messageResponseContent}");
                }

                // Run the assistant
                var runRequest = new
                {
                    assistant_id = assistantId
                };
                var runContent = JsonSerializer.Serialize(runRequest);

                using var runRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://api.openai.com/v1/threads/{threadId}/runs");
                runRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                runRequestMessage.Headers.Add("OpenAI-Beta", "assistants=v2");
                runRequestMessage.Content = new StringContent(runContent, Encoding.UTF8, "application/json");

                var runResponse = await _httpClient.SendAsync(runRequestMessage);
                var runResponseContent = await runResponse.Content.ReadAsStringAsync();

                if (!runResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to start run: {runResponse.StatusCode}, Body: {runResponseContent}");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var runData = await JsonSerializer.DeserializeAsync<RunResponse>(await runResponse.Content.ReadAsStreamAsync(), options);
                var runId = runData?.Id ?? throw new InvalidOperationException("Failed to get run ID");

                var maxAttempts = 30;
                var attempts = 0;

                while (attempts < maxAttempts)
                {
                    await Task.Delay(1000); // Wait 1 second

                    using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}");
                    statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    statusRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                    var statusResponse = await _httpClient.SendAsync(statusRequest);
                    var statusContent = await statusResponse.Content.ReadAsStringAsync();

                    if (!statusResponse.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"Failed to get run status: {statusResponse.StatusCode}, Body: {statusContent}");
                    }
                    var statusData = JsonSerializer.Deserialize<RunResponse>(statusContent, options);

                    if (statusData?.Status == "completed")
                    {
                        break;
                    }
                    else if (statusData?.Status == "requires_action" && statusData.Required_Action?.Type == "submit_tool_outputs")
                    {
                        // Handle function calls
                        var toolOutputs = new List<object>();

                        foreach (var toolCall in statusData.Required_Action.Submit_Tool_Outputs?.Tool_Calls ?? new List<ToolCall>())
                        {
                            var toolCallId = toolCall.Id;
                            var functionCall = toolCall.Function;

                            switch (functionCall.Name)
                            {
                                case "GetCurrentDate":
                                    var currentDateResponse = GetCurrentDateFunction();
                                    var currentDateJson = JsonSerializer.Serialize(currentDateResponse, new JsonSerializerOptions
                                    {
                                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                    });
                                    toolOutputs.Add(new
                                    {
                                        tool_call_id = toolCallId,
                                        output = currentDateJson
                                    });
                                    break;

                                case "CheckBookingTimeAvailability":
                                    if (!string.IsNullOrEmpty(functionCall.Arguments))
                                    {
                                        var bookingAvailabilityData = JsonSerializer.Deserialize<CheckBookingTimeAvailabilityRequest>(functionCall.Arguments, new JsonSerializerOptions
                                        {
                                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                        });

                                        if (bookingAvailabilityData != null)
                                        {
                                            var bookingAvailabilityResponse = await CheckBookingTimeAvailabilityFunction(bookingAvailabilityData);
                                            var bookingAvailabilityJson = JsonSerializer.Serialize(bookingAvailabilityResponse, new JsonSerializerOptions
                                            {
                                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                            });
                                            toolOutputs.Add(new
                                            {
                                                tool_call_id = toolCallId,
                                                output = bookingAvailabilityJson
                                            });
                                        }
                                    }
                                    break;

                                case "AddAppointment":
                                    if (!string.IsNullOrEmpty(functionCall.Arguments))
                                    {
                                        var appointmentData = JsonSerializer.Deserialize<AppointmentRequest>(functionCall.Arguments, new JsonSerializerOptions
                                        {
                                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                        });

                                        if (appointmentData != null)
                                        {
                                            var appointmentResponse = await AddAppointmentFunction(appointmentData);
                                            var appointmentJson = JsonSerializer.Serialize(appointmentResponse, new JsonSerializerOptions
                                            {
                                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                            });
                                            toolOutputs.Add(new
                                            {
                                                tool_call_id = toolCallId,
                                                output = appointmentJson
                                            });
                                        }
                                    }
                                    break;
                            }
                        }

                        // Submit tool outputs
                        var submitRequest = new
                        {
                            tool_outputs = toolOutputs
                        };
                        var submitJson = JsonSerializer.Serialize(submitRequest);

                        using var submitRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}/submit_tool_outputs");
                        submitRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        submitRequestMessage.Headers.Add("OpenAI-Beta", "assistants=v2");
                        submitRequestMessage.Content = new StringContent(submitJson, Encoding.UTF8, "application/json");

                        var submitResponse = await _httpClient.SendAsync(submitRequestMessage);
                        var submitContent = await submitResponse.Content.ReadAsStringAsync();

                        if (!submitResponse.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException($"Failed to submit tool outputs: {submitResponse.StatusCode}, Body: {submitContent}");
                        }

                        // Reset attempts to wait for completion
                        attempts = 0;
                        continue;
                    }
                    else if (statusData?.Status == "failed" || statusData?.Status == "cancelled")
                    {
                        throw new InvalidOperationException($"Run failed with status: {statusData.Status}");
                    }

                    attempts++;
                }

                if (attempts >= maxAttempts)
                {
                    throw new InvalidOperationException("Run timed out");
                }

                // Get the response messages
                using var messagesRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.openai.com/v1/threads/{threadId}/messages");
                messagesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                messagesRequest.Headers.Add("OpenAI-Beta", "assistants=v2");

                var messagesResponse = await _httpClient.SendAsync(messagesRequest);
                var messagesContent = await messagesResponse.Content.ReadAsStringAsync();

                if (!messagesResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to get messages: {messagesResponse.StatusCode}, Body: {messagesContent}");
                }

                var messagesData = JsonSerializer.Deserialize<MessagesResponse>(messagesContent, options);
                var assistantMessage = messagesData?.Data?.FirstOrDefault(m => m.Role == "assistant");

                if (assistantMessage == null)
                {
                    throw new InvalidOperationException("No assistant response found");
                }

                return new OpenAIResponse
                {
                    Message = assistantMessage.Content?.FirstOrDefault()?.Text?.Value ?? "No response content"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                return new OpenAIResponse
                {
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        private GetCurrentDateResponse GetCurrentDateFunction()
        {
            var now = DateTime.Now;
            return new GetCurrentDateResponse
            {
                CurrentDate = now.ToString("yyyy-MM-dd"),
                CurrentTime = now.ToString("HH:mm"),
                DayOfWeek = now.ToString("dddd"),
                Message = $"Today is {now:dddd, MMMM dd, yyyy} and the current time is {now:HH:mm}."
            };
        }

        private async Task<CheckBookingTimeAvailabilityResponse> CheckBookingTimeAvailabilityFunction(CheckBookingTimeAvailabilityRequest request)
        {
            try
            {
                if (!DateTime.TryParse(request.Date, out var date))
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = "Invalid date format. Please use YYYY-MM-DD format."
                    };
                }

                if (!TimeSpan.TryParse(request.Time, out var time))
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = "Invalid time format. Please use HH:MM format."
                    };
                }

                var requestedDateTime = date.Add(time);
                var normalizedDateTime = NormalizeTo30MinuteSlot(requestedDateTime);

                // Convert to UTC for database operations to satisfy PostgreSQL 'timestamptz' requirement
                var normalizedDateTimeUtc = ConvertToUtc(normalizedDateTime);

                // Enforce future-only, valid business days (Mon-Sat), and business hours (09:00–17:00, last start 16:30)
                var now = DateTime.Now;
                if (normalizedDateTime < now)
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = $"The time slot {normalizedDateTime:hh:mm tt} on {normalizedDateTime:MMMM dd, yyyy} is in the past. Please choose a future time.",
                        RequestedDateTime = requestedDateTime,
                        NormalizedDateTime = normalizedDateTime
                    };
                }

                if (normalizedDateTime.DayOfWeek == DayOfWeek.Sunday)
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = "The clinic is closed on Sunday. Please choose Monday–Saturday within 09:00–17:00.",
                        RequestedDateTime = requestedDateTime,
                        NormalizedDateTime = normalizedDateTime
                    };
                }

                var start = new TimeSpan(9, 0, 0);
                var lastStart = new TimeSpan(16, 30, 0); // last 30-min slot starts at 16:30
                if (normalizedDateTime.TimeOfDay < start || normalizedDateTime.TimeOfDay > lastStart)
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = "Please choose a time between 09:00 and 17:00 (last start 16:30), Monday–Saturday.",
                        RequestedDateTime = requestedDateTime,
                        NormalizedDateTime = normalizedDateTime
                    };
                }

                // Check if there's already a booking for this 30-minute slot
                var existingBooking = await _bookingService.CheckBookingTimeAvailabilityAsync(normalizedDateTimeUtc);

                if (existingBooking)
                {
                    return new CheckBookingTimeAvailabilityResponse
                    {
                        IsAvailable = false,
                        Message = $"Sorry, the time slot {normalizedDateTime:hh:mm tt} on {normalizedDateTime:MMMM dd, yyyy} is already booked.",
                        RequestedDateTime = requestedDateTime,
                        NormalizedDateTime = normalizedDateTime
                    };
                }

                return new CheckBookingTimeAvailabilityResponse
                {
                    IsAvailable = true,
                    Message = $"Great! The time slot {normalizedDateTime:hh:mm tt} on {normalizedDateTime:MMMM dd, yyyy} is available for booking.",
                    RequestedDateTime = requestedDateTime,
                    NormalizedDateTime = normalizedDateTime
                };
            }
            catch (Exception ex)
            {
                return new CheckBookingTimeAvailabilityResponse
                {
                    IsAvailable = false,
                    Message = "Sorry, I encountered an error while checking booking availability. Please try again."
                };
            }
        }

        /// <summary>
        /// Normalizes a DateTime to the nearest 30-minute slot
        /// </summary>
        private static DateTime NormalizeTo30MinuteSlot(DateTime dateTime)
        {
            var minutes = dateTime.Minute;
            var normalizedMinutes = (minutes / 30) * 30; // Rounds down to nearest 30-minute slot

            return new DateTime(
                dateTime.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                normalizedMinutes,
                0,
                dateTime.Kind
            );
        }

        private async Task<AppointmentResponse> AddAppointmentFunction(AppointmentRequest request)
        {
            try
            {
                if (!DateTime.TryParse(request.Date, out var date))
                {
                    return new AppointmentResponse
                    {
                        Success = false,
                        Message = "Invalid date format. Please use YYYY-MM-DD format."
                    };
                }

                if (!TimeSpan.TryParse(request.Time, out var time))
                {
                    return new AppointmentResponse
                    {
                        Success = false,
                        Message = "Invalid time format. Please use HH:MM format."
                    };
                }

                var appointmentLocalDateTime = date.Add(time);

                // Enforce future-only, valid business days (Mon-Sat), and business hours (09:00–17:00, last start 16:30)
                if (appointmentLocalDateTime < DateTime.Now)
                {
                    return new AppointmentResponse
                    {
                        Success = false,
                        Message = "Cannot book an appointment in the past. Please choose a future time."
                    };
                }

                if (appointmentLocalDateTime.DayOfWeek == DayOfWeek.Sunday)
                {
                    return new AppointmentResponse
                    {
                        Success = false,
                        Message = "The clinic is closed on Sunday. Please choose Monday–Saturday within 09:00–17:00."
                    };
                }

                var start = new TimeSpan(9, 0, 0);
                var lastStart = new TimeSpan(16, 30, 0);
                if (appointmentLocalDateTime.TimeOfDay < start || appointmentLocalDateTime.TimeOfDay > lastStart)
                {
                    return new AppointmentResponse
                    {
                        Success = false,
                        Message = "Appointments are available 09:00–17:00 with last start at 16:30, Monday–Saturday."
                    };
                }

                // Convert to UTC for database storage
                var appointmentUtcDateTime = ConvertToUtc(appointmentLocalDateTime);

                var createBookingDto = new DTOs.CreateBookingDto
                {
                    PatientName = request.Name,
                    PhoneNumber = request.PhoneNumber,
                    DateTime = appointmentUtcDateTime,
                    ReasonForVisit = request.ReasonForVisit
                };

                var booking = await _bookingService.CreateBookingAsync(createBookingDto);

                return new AppointmentResponse
                {
                    Success = true,
                    Message = $"Great! I've successfully booked your appointment for {request.Name} on {appointmentLocalDateTime:MMMM dd, yyyy} at {appointmentLocalDateTime:hh:mm tt}" +
                              (string.IsNullOrEmpty(request.ReasonForVisit) ? "." : $" for {request.ReasonForVisit}."),
                    BookingId = booking.Id
                };
            }
            catch (InvalidOperationException ex)
            {
                return new AppointmentResponse
                {
                    Success = false,
                    Message = $"Sorry, I couldn't book the appointment: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new AppointmentResponse
                {
                    Success = false,
                    Message = "Sorry, I encountered an error while booking the appointment. Please try again."
                };
            }
        }

        private static DateTime ConvertToUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }

            if (dateTime.Kind == DateTimeKind.Local)
            {
                return dateTime.ToUniversalTime();
            }

            // Treat unspecified as local time and convert to UTC
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
        }
    }
}
