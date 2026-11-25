namespace Shared;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Rejected
}

public sealed class EventItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public int Capacity { get; set; }
    public int AvailableTickets { get; set; }
}

public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed record CreateBookingRequest(Guid EventId, string UserId, int Quantity);

public sealed record BookingMessage(Guid BookingId, Guid EventId, string UserId, int Quantity);
