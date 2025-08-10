using DentistDemo.Backend.Data;
using DentistDemo.Backend.DTOs;
using DentistDemo.Backend.Interfaces;
using DentistDemo.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace DentistDemo.Backend.Services
{
    public class BookingService : IBookingService
    {
        private readonly DentistDbContext _context;

        public BookingService(
            DentistDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BookingResponseDto>> GetBookingsAsync()
        {
            var bookings = await _context.Bookings
                .OrderBy(b => b.DateTime)
                .ToListAsync();

            return bookings.Select(MapToResponseDto);
        }

        public async Task<BookingResponseDto> CreateBookingAsync(CreateBookingDto dto)
        {
            // Normalize the booking time to 30-minute slots
            var normalizedDateTime = NormalizeTo30MinuteSlot(dto.DateTime);

            // Check if there's already a booking for this 30-minute slot
            var existingBooking = await _context.Bookings
                .AnyAsync(b => b.DateTime == normalizedDateTime &&
                              b.Status != BookingStatus.Cancelled);

            if (existingBooking)
            {
                throw new InvalidOperationException("This time slot is already booked.");
            }

            var booking = new Booking
            {
                PatientName = dto.PatientName,
                PhoneNumber = dto.PhoneNumber,
                DateTime = normalizedDateTime,
                ReasonForVisit = dto.ReasonForVisit ?? string.Empty,
                Status = BookingStatus.Pending
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            var bookingResponse = MapToResponseDto(booking);

            return bookingResponse;
        }

        public async Task<bool> CancelBookingAsync(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
                return false;

            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var bookingResponse = MapToResponseDto(booking);

            return true;
        }

        public async Task<bool> CheckBookingTimeAvailabilityAsync(DateTime dateTime)
        {
            // Check if there's already a booking for this 30-minute slot
            var existingBooking = await _context.Bookings
                .AnyAsync(b => b.DateTime == dateTime &&
                              b.Status != BookingStatus.Cancelled);

            return existingBooking; // Returns true if slot is booked, false if available
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

        private static BookingResponseDto MapToResponseDto(Booking booking)
        {
            return new BookingResponseDto
            {
                Id = booking.Id,
                PatientName = booking.PatientName,
                PhoneNumber = booking.PhoneNumber,
                DateTime = booking.DateTime,
                Status = booking.Status,
                ReasonForVisit = booking.ReasonForVisit,
                CreatedAt = booking.CreatedAt,
                UpdatedAt = booking.UpdatedAt
            };
        }
    }
}
