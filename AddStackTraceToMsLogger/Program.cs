using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

var builder = Host.CreateApplicationBuilder(args);
{
	builder.Logging
		.AddOpenTelemetry(options =>
		{
			options.IncludeScopes = true;
			options.IncludeFormattedMessage = true;
		});

	builder.Services
		.AddLoggerDecoration()
		.AddOpenTelemetry()
		.WithLogging(loggerBuilder => loggerBuilder.AddConsoleExporter());
}

using var app = builder.Build();

await app.StartAsync();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Hello, World!");

Console.ReadKey(false);

await app.StopAsync();
