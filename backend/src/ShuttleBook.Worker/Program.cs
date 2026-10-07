using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddShuttleBookDatabase(builder.Configuration);
builder.Services.AddHostedService<FoundationWorker>();
builder.Services.AddHostedService<ApprovalOutboxWorker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<BookingExpiryWorker>();
builder.Services.AddHostedService<ConfirmationAlertWorker>();
await builder.Build().RunAsync();
