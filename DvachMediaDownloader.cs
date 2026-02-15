namespace DvachMediaDownloader;

public interface IDvachMediaDownloader
{
	Task DownloadMedia(Uri threadUri);
}

public sealed class DvachMediaDownloader : IDvachMediaDownloader
{
	private readonly IDvachThreadParser _threadParser;
	private readonly IDvachHttpClient _dvachHttpClient;
	private readonly IDvachPathsManager _pathsManager;
	private readonly ILogger? _logger;

	public DvachMediaDownloader(IDvachThreadParser threadParser,
		IDvachHttpClient dvachHttpClient, IDvachPathsManager pathsManager, ILogger? logger)
	{
		_threadParser = threadParser;
		_dvachHttpClient = dvachHttpClient;
		_pathsManager = pathsManager;
		_logger = logger;
	}

	public async Task DownloadMedia(Uri threadUri)
	{
		var threadParserResult = await _threadParser.GetMediaUrisFromThread(threadUri);

		var targetDirectoryPath = _pathsManager.GetThreadWorkingDirectoryPath(threadParserResult.ThreadId,
			threadParserResult.ThreadName, threadParserResult.ThreadStartTime);
		Directory.CreateDirectory(targetDirectoryPath);

		foreach (var mediaUri in threadParserResult.MediaUris)
		{
			var targetFilePath = _pathsManager.GetMediaFilePath(targetDirectoryPath, mediaUri);
			if (File.Exists(targetFilePath))
			{
				_logger?.Information($"Skipping '{mediaUri}' as existing at '{targetFilePath}'.");
				continue;
			}

			try
			{
				await _dvachHttpClient.DownloadFile(mediaUri, targetFilePath);
			}
			catch (Exception ex)
			{
				_logger?.Error(ex, $"Error downloading media file from '{mediaUri}' to '{targetFilePath}'.");
				File.Delete(targetFilePath);
			}
		}
	}
}
