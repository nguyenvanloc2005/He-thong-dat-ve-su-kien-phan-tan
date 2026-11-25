using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TicketDatabase")
        ?? "Data Source=/data/tickets.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    db.Database.EnsureCreated();

    if (!await db.Events.AnyAsync())
    {
        db.Events.AddRange(
            new EventItem
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Vietnam Tech Conference",
                Description = "Conference on distributed systems and cloud technologies",
                StartsAt = DateTime.UtcNow.AddDays(30),
                Capacity = 500,
                AvailableTickets = 500
            },
            new EventItem
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Music Night",
                Description = "Live music and cultural festival",
                StartsAt = DateTime.UtcNow.AddDays(15),
                Capacity = 200,
                AvailableTickets = 200
            });
        await db.SaveChangesAsync();
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "ticket-api",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/events", async (TicketDbContext db) =>
    Results.Ok(await db.Events.AsNoTracking().OrderBy(e => e.StartsAt).ToListAsync()));

app.MapGet("/events/{id:guid}", async (Guid id, TicketDbContext db) =>
    await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id)
        is { } eventItem
        ? Results.Ok(eventItem)
        : Results.NotFound());

app.MapPost("/bookings", async (CreateBookingRequest request, TicketDbContext db, IConfiguration config, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("TicketApi");
    if (string.IsNullOrWhiteSpace(request.UserId))
        return Results.BadRequest(new { error = "UserId is required" });
    if (request.Quantity <= 0)
        return Results.BadRequest(new { error = "Quantity must be greater than zero" });

    var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId);
    if (eventItem is null)
        return Results.NotFound(new { error = "Event not found" });

    var booking = new Booking
    {
        Id = Guid.NewGuid(),
        EventId = request.EventId,
        UserId = request.UserId.Trim(),
        Quantity = request.Quantity,
        Status = BookingStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    db.Bookings.Add(booking);
    await db.SaveChangesAsync();

    try
    {
        var rabbit = config.GetSection("RabbitMQ");
        var factory = new ConnectionFactory
        {
            HostName = rabbit["Host"] ?? "rabbitmq",
            Port = int.TryParse(rabbit["Port"], out var port) ? port : 5672,
            UserName = rabbit["User"] ?? "guest",
            Password = rabbit["Password"] ?? "guest"
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare("booking.requests", durable: true, exclusive: false, autoDelete: false);

        var message = new BookingMessage(booking.Id, booking.EventId, booking.UserId, booking.Quantity);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.CorrelationId = booking.Id.ToString();
        channel.BasicPublish("", "booking.requests", properties, body);

        logger.LogInformation("Published booking {BookingId} for event {EventId}", booking.Id, booking.EventId);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Booking {BookingId} remains Pending because RabbitMQ is unavailable", booking.Id);
    }

    return Results.Accepted($"/bookings/{booking.Id}", booking);
});

app.MapGet("/bookings/{id:guid}", async (Guid id, TicketDbContext db) =>
    await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id)
        is { } booking
        ? Results.Ok(booking)
        : Results.NotFound());

app.MapGet("/users/{userId}/bookings", async (string userId, TicketDbContext db) =>
    Results.Ok(await db.Bookings.AsNoTracking()
        .Where(b => b.UserId == userId)
        .OrderByDescending(b => b.CreatedAt)
        .ToListAsync()));

app.Run();
