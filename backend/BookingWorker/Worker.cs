using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;

namespace BookingWorker;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IModel? channel = null;

            try
            {
                var rabbit = configuration.GetSection("RabbitMQ");
                var factory = new ConnectionFactory
                {
                    HostName = rabbit["Host"] ?? "rabbitmq",
                    Port = int.TryParse(rabbit["Port"], out var port) ? port : 5672,
                    UserName = rabbit["User"] ?? "guest",
                    Password = rabbit["Password"] ?? "guest",
                    DispatchConsumersAsync = true
                };

                connection = factory.CreateConnection();
                channel = connection.CreateModel();
                channel.QueueDeclare("booking.requests", durable: true, exclusive: false, autoDelete: false);
                channel.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, args) =>
                {
                    try
                    {
                        await ProcessMessageAsync(args.Body.ToArray(), stoppingToken);
                        channel.BasicAck(args.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process booking message");
                        channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                channel.BasicConsume("booking.requests", autoAck: false, consumer);
                logger.LogInformation("Booking worker connected to RabbitMQ");

                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ unavailable; retrying in 5 seconds");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                channel?.Dispose();
                connection?.Dispose();
            }
        }
    }

    private async Task ProcessMessageAsync(byte[] body, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<BookingMessage>(Encoding.UTF8.GetString(body));
        if (message is null)
            throw new InvalidDataException("Invalid booking message");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == message.BookingId, cancellationToken);
        if (booking is null)
        {
            logger.LogWarning("Booking {BookingId} not found", message.BookingId);
            return;
        }
        if (booking.Status != BookingStatus.Pending)
        {
            logger.LogInformation("Booking {BookingId} already processed as {Status}", booking.Id, booking.Status);
            return;
        }

        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == message.EventId, cancellationToken);
        if (eventItem is null)
        {
            await RejectAsync(db, booking, "Event not found", cancellationToken);
        }
        else if (eventItem.AvailableTickets < message.Quantity)
        {
            await RejectAsync(db, booking, "Not enough tickets available", cancellationToken);
        }
        else
        {
            eventItem.AvailableTickets -= message.Quantity;
            booking.Status = BookingStatus.Confirmed;
            booking.RejectionReason = null;
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Confirmed booking {BookingId}; remaining tickets: {Remaining}",
                booking.Id, eventItem.AvailableTickets);
        }
    }

    private async Task RejectAsync(
        TicketDbContext db,
        Booking booking,
        string reason,
        CancellationToken cancellationToken)
    {
        booking.Status = BookingStatus.Rejected;
        booking.RejectionReason = reason;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rejected booking {BookingId}: {Reason}", booking.Id, reason);
    }
}
