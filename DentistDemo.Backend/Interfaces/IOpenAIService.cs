using DentistDemo.Backend.Models;

namespace DentistDemo.Backend.Interfaces
{
    public class GetCurrentDateResponse
    {
        public string CurrentDate { get; set; } = string.Empty; // YYYY-MM-DD format
        public string CurrentTime { get; set; } = string.Empty; // HH:MM format
        public string DayOfWeek { get; set; } = string.Empty; // Full day name
        public string Message { get; set; } = string.Empty;
    }

    public class CheckBookingTimeAvailabilityRequest
    {
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD format
        public string Time { get; set; } = string.Empty; // HH:MM format
    }

    public class CheckBookingTimeAvailabilityResponse
    {
        public bool IsAvailable { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? RequestedDateTime { get; set; }
        public DateTime? NormalizedDateTime { get; set; }
    }

    public class AppointmentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD format
        public string Time { get; set; } = string.Empty; // HH:MM format
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ReasonForVisit { get; set; } // Made optional
    }

    public class AppointmentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? BookingId { get; set; }
    }

    public class OpenAIResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public interface IOpenAIService
    {
        Task<ThreadResponse> CreateThread();
        Task<OpenAIResponse> SendMessageToAssistantAsync(string message, string threadId);
    }
}
