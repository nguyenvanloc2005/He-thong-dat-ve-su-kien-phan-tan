using BookingWorker;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TicketDatabase")
        ?? "Data Source=/data/tickets.db"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    db.Database.EnsureCreated();
}
host.Run();
