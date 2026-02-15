using AngleSharp;

namespace DvachMediaDownloader;

public interface IDvachThreadParser
{
	public class ThreadParserResult
	{
		public required Uri ThreadUri { get; set; }
		public required IEnumerable<Uri> MediaUris { get; set; }
		public required string ThreadId { get; set; }
		public string? ThreadName { get; set; }
		public DateTime? ThreadStartTime { get; set; }
	}

	Task<ThreadParserResult> GetMediaUrisFromThread(Uri threadUri);
}

public sealed class DvachThreadParser : IDvachThreadParser
{
	private readonly IDvachHttpClient _dvachHttpClient;
	private readonly ILogger? _logger;
	public List<string> MediaFileExtensions { get; set; } = ["mp4", "webm", "jpeg", "jpg", "png", "gif", "bmp", "webp"];

	public DvachThreadParser(IDvachHttpClient dvachHttpClient, ILogger? logger)
	{
		_dvachHttpClient = dvachHttpClient;
		_logger = logger;
	}

	public async Task<IDvachThreadParser.ThreadParserResult> GetMediaUrisFromThread(Uri threadUri)
	{
		var threadHtml = await _dvachHttpClient.DownloadThreadHtml(threadUri);

		using var browsingContext = BrowsingContext.New(Configuration.Default);
		var document = await browsingContext.OpenAsync(
			req => req.Content(threadHtml).Address(threadUri.ToString()));

		var mediaUriStrings = document.QuerySelectorAll("a.post__image-link")
				.Select(a => a.GetAttribute("href"))
				.Where(href => !string.IsNullOrWhiteSpace(href))
				.Select(x => x!)
				.ToList();

		_logger?.Information($"Found {mediaUriStrings.Count} media links: [{string.Join(',', mediaUriStrings.Select(x => $"'{x}'"))}]");

		var lastSlashIndexInThreadUri = threadUri.OriginalString.LastIndexOf('/');
		var lastDotIndexInThreadUri = threadUri.OriginalString.LastIndexOf('.');
		var threadId = threadUri.OriginalString.Substring(lastSlashIndexInThreadUri + 1,
			lastDotIndexInThreadUri - lastSlashIndexInThreadUri - 1); // TODO

		var threadName = document.QuerySelector("span.post__title")?.InnerHtml.Trim();
		var threadStartTimeString = document.QuerySelector("span.post__time")?.InnerHtml;
		DateTime.TryParseExact(threadStartTimeString?.Substring(0, "dd/MM/yy".Length),
			"dd/MM/yy", default, default, out var threadStartTime);

		var firstSlashIndexInThreadUri = threadUri.OriginalString.IndexOf('/', "https://".Length + 1);
		var baseUri = threadUri.OriginalString.Substring(0, firstSlashIndexInThreadUri);
		var mediaUris = mediaUriStrings.Select(href => new Uri(baseUri + href)).ToList();
		return new IDvachThreadParser.ThreadParserResult
		{
			ThreadUri = threadUri,
			MediaUris = mediaUris,
			ThreadId = threadId,
			ThreadName = threadName,
			ThreadStartTime = threadStartTime.Ticks == 0 ? null : threadStartTime,
		};
	}
}
