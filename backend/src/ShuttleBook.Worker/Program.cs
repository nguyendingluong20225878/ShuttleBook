using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddShuttleBookDatabase(builder.Configuration);
builder.Services.AddHostedService<FoundationWorker>();
builder.Services.AddHostedService<ApprovalOutboxWorker>();
await builder.Build().RunAsync();
