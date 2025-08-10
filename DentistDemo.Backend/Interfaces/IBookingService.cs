using DentistDemo.Backend.DTOs;

namespace DentistDemo.Backend.Interfaces
{
    public interface IBookingService
    {
        Task<IEnumerable<BookingResponseDto>> GetBookingsAsync();
        Task<BookingResponseDto> CreateBookingAsync(CreateBookingDto dto);
        Task<bool> CancelBookingAsync(int id);
        Task<bool> CheckBookingTimeAvailabilityAsync(DateTime dateTime);
    }
}
