using System.Text.RegularExpressions;

namespace DvachMediaDownloader;

public interface IDvachEntryPoint
{
	Task Run();
}

public interface IDvachConsoleUI
{
	Task Interact();
}

public sealed class DvachConsoleUI : IDvachConsoleUI, IDvachEntryPoint
{
	private readonly IDvachMediaDownloader _mediaDownloader;
	private readonly ILogger? _logger;

	public DvachConsoleUI(IDvachMediaDownloader mediaDownloader, ILogger? logger)
	{
		_mediaDownloader = mediaDownloader;
		_logger = logger;
	}

	Task IDvachEntryPoint.Run() => Interact();

	public static readonly Regex ThreadUriRegex = new(@"2ch[.]\w+\/\w+\/res\/\d+[.]html");
	public async Task Interact()
	{
		Console.Write("Enter 2ch thread URIs: ");
		var threadUrisInput = Console.ReadLine() ?? "";
		var threadUris = ThreadUriRegex.Matches(threadUrisInput)
			.Distinct().Select(x => new Uri($"https://{x}")).ToList();

		_logger?.Information($"Found {threadUris.Count} thread links: " +
			$"[{string.Join(", ", threadUris.Select(x => $"'{x}'"))}]");

		foreach(var threadUri in threadUris)
		{
			try
			{
				_logger?.Information($"Begin processing thread '{threadUri}'...");
				await _mediaDownloader.DownloadMedia(threadUri);
				_logger?.Information($"Finished processing thread '{threadUri}'.");
			}
			catch (Exception ex)
			{
				_logger?.Error(ex, $"Error processing thread '{threadUri}'.");
			}
		}
	}
}
