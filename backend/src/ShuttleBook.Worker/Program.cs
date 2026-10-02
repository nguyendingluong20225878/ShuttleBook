using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddShuttleBookDatabase(builder.Configuration);
builder.Services.AddHostedService<FoundationWorker>();
await builder.Build().RunAsync();
