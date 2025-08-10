using DentistDemo.Backend.Models;

namespace DentistDemo.Backend.DTOs
{
    public class CreateBookingDto
    {
        public string PatientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public string? ReasonForVisit { get; set; }
    }

    public class BookingResponseDto
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public BookingStatus Status { get; set; }
        public string? ReasonForVisit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class WhatsAppBookingRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
    }
}
