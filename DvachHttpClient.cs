using System.Text.RegularExpressions;

namespace DvachMediaDownloader;

public interface IDvachHttpClient
{
	Task<string> DownloadThreadHtml(Uri threadUri);
	Task DownloadFile(Uri fileUri, string destinationFilePath);
}

public sealed class DvachHttpClient : IDvachHttpClient
{
	public static readonly Regex SearchThreadUriRegex = new(@"2ch[.]\w+/\w+/res/(0-9)+[.]html");
	public static readonly Regex ValidationThreadUriRegex = new(@"^2ch[.]\w+/\w+/res/(0-9)+[.]html$");

	private readonly HttpClient _httpClient;
	private readonly ILogger? _logger;

	public DvachHttpClient(HttpClient httpClient, ILogger? logger)
	{
		_httpClient = httpClient;
		_logger = logger;
	}

	public async Task<string> DownloadThreadHtml(Uri threadUri)
	{
		_logger?.Information($"Downloading thread html from '{threadUri}'...");
		var threadHtml = await _httpClient.GetStringAsync(threadUri);
		_logger?.Information($"Downloaded ~{threadHtml.Length/1024d/1024d:0.000} MiB.");
		return threadHtml;
	}

	public async Task DownloadFile(Uri fileUri, string destinationFilePath)
	{
		_logger?.Information($"Requesting file from '{fileUri}'...");
		var requestStarted = DateTime.UtcNow;
		using var response = await _httpClient.GetAsync(fileUri, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		await using var fs = new FileStream(destinationFilePath, new FileStreamOptions
		{
			Access = FileAccess.Write,
			Mode = FileMode.CreateNew,
			Share = FileShare.Read,
			Options = FileOptions.Asynchronous,
			PreallocationSize = default,
			BufferSize = 512 * 1024,
		});
		_logger?.Information($"Streaming into '{destinationFilePath}'...");
		await response.Content.CopyToAsync(fs);
		var requestTookSeconds = (DateTime.UtcNow - requestStarted).TotalSeconds;
		_logger?.Information($"Done: {fs.Length/1024d/1024d:0.000} MiB in {requestTookSeconds:0.000} seconds.");
	}
}
