var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<VersionLoggerService>();
var host = builder.Build();
host.Run();
