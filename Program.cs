global using ILogger = Serilog.ILogger;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DvachMediaDownloader;

static class Program
{
	static async Task Main()
	{
		try { await MainPrivate(); }
		finally
		{
			Console.WriteLine("Press enter to exit...");
			Console.ReadLine();
		}
	}

	static async Task MainPrivate()
	{
		var services = new ServiceCollection();
		services.AddSerilog((sp, configure) =>
		{
			configure.WriteTo.Console();
			configure.WriteTo.File(sp.GetRequiredService<IServicePathsManager>().GetLogFilePath(),
				fileSizeLimitBytes: 384 * 1024 * 1024,
				rollOnFileSizeLimit: true,
				retainedFileCountLimit: 2
			);
		});
		services.AddSingleton<DvachPathsManager>();
		services.AddSingleton<IDvachPathsManager>(provider => provider.GetRequiredService<DvachPathsManager>());
		services.AddSingleton<IServicePathsManager>(provider => provider.GetRequiredService<DvachPathsManager>());
		services.AddScoped<HttpClient>((_) => new HttpClient());
		services.AddScoped<IDvachHttpClient, DvachHttpClient>();
		services.AddScoped<IDvachThreadParser, DvachThreadParser>();
		services.AddScoped<IDvachThreadParser, ArhivachThreadParser>();
		services.AddScoped<IDvachMediaDownloader, DvachMediaDownloader>();

		services.AddScoped<DvachConsoleUI>();
		services.AddScoped<IDvachConsoleUI>(provider => provider.GetRequiredService<DvachConsoleUI>());
		services.AddScoped<IDvachEntryPoint>(provider => provider.GetRequiredService<DvachConsoleUI>());

		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var service = scope.ServiceProvider.GetRequiredService<IDvachEntryPoint>();

		await service.Run();
	}
}