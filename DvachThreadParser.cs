using AngleSharp;
using System.Text.RegularExpressions;

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

	bool CanProcessThread(Uri threadUri);
	Task<ThreadParserResult> GetMediaUrisFromThread(Uri threadUri);
}

// TODO: template method from both classes or decompose to reuse the same parts

public sealed class DvachThreadParser : IDvachThreadParser
{
	private readonly IDvachHttpClient _dvachHttpClient;
	private readonly ILogger? _logger;

	public DvachThreadParser(IDvachHttpClient dvachHttpClient, ILogger? logger)
	{
		_dvachHttpClient = dvachHttpClient;
		_logger = logger;
	}

	private static readonly Regex _supportedThreadUriRegex = new(@"2ch[.]\w+/\w+/res/\d+[.]html");
	public bool CanProcessThread(Uri threadUri) => _supportedThreadUriRegex.IsMatch(threadUri.OriginalString);
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

		var firstThreadPost = document.QuerySelector(".post");
		var threadName = GetThreadName([
			() => firstThreadPost?.QuerySelector(".post__title")?.InnerHtml.Trim(),
			() => firstThreadPost?.QuerySelector(".post__message")?.InnerHtml.Trim(),
			() => firstThreadPost?.QuerySelector(".post__message_op")?.InnerHtml.Trim(),
		]);
		var threadStartTimeString = firstThreadPost?.QuerySelector("span.post__time")?.InnerHtml;
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

	private static readonly IReadOnlySet<char> fileNameProhibitedChars = Path.GetInvalidFileNameChars().ToHashSet();
	private string? GetThreadName(IEnumerable<Func<string?>> candidatesFactory)
	{
		return candidatesFactory.Select(c=> c()).Where(c => !string.IsNullOrWhiteSpace(c))?
			.Select(c => string.Concat(c!.Select(x => fileNameProhibitedChars.Contains(x) ? ' ' : x).Take(64)))
			.FirstOrDefault();
	}
}

public sealed class ArhivachThreadParser : IDvachThreadParser
{
	private readonly IDvachHttpClient _dvachHttpClient;
	private readonly ILogger? _logger;

	public ArhivachThreadParser(IDvachHttpClient dvachHttpClient, ILogger? logger)
	{
		_dvachHttpClient = dvachHttpClient;
		_logger = logger;
	}


	private static readonly Regex _supportedThreadUriRegex = new(@"arhivach[.]\w+/thread/\d+");
	public bool CanProcessThread(Uri threadUri) => _supportedThreadUriRegex.IsMatch(threadUri.OriginalString);
	public async Task<IDvachThreadParser.ThreadParserResult> GetMediaUrisFromThread(Uri threadUri)
	{
		var threadHtml = await _dvachHttpClient.DownloadThreadHtml(threadUri);

		using var browsingContext = BrowsingContext.New(Configuration.Default);
		var document = await browsingContext.OpenAsync(
			req => req.Content(threadHtml).Address(threadUri.ToString()));

		var mediaUriStrings = document.QuerySelectorAll("a.img_filename")
				.Select(a => a.GetAttribute("href"))
				.Where(href => !string.IsNullOrWhiteSpace(href))
				.Select(x => x!)
				.ToList();

		_logger?.Information($"Found {mediaUriStrings.Count} media links: [{string.Join(',', mediaUriStrings.Select(x => $"'{x}'"))}]");

		var lastSlashIndexInThreadUri = threadUri.OriginalString.LastIndexOf('/');
		var threadId = threadUri.OriginalString.Substring(lastSlashIndexInThreadUri + 1,
			threadUri.OriginalString.Length - lastSlashIndexInThreadUri - 1);

		var firstThreadPost = document.QuerySelector(".post");
		var threadName = GetThreadName([
			() => firstThreadPost?.QuerySelector(".post_subject")?.InnerHtml.Trim(),
			() => firstThreadPost?.QuerySelector(".post_comment_body")?.InnerHtml.Trim(),
		]);
		var threadStartTimeString = firstThreadPost?.QuerySelector("span.post_time")?.InnerHtml;
		DateTime.TryParseExact(threadStartTimeString?.Substring(0, "dd/MM/yy".Length),
			"dd/MM/yy", default, default, out var threadStartTime);

		var firstSlashIndexInThreadUri = threadUri.OriginalString.IndexOf('/', "https://".Length + 1);
		var baseUri = threadUri.OriginalString.Substring(0, firstSlashIndexInThreadUri);
		var mediaUris = mediaUriStrings.Select(href 
			// unlike 2ch, refs may be absolute here
			=> href.StartsWith('/') ? new Uri(baseUri + href) : new Uri(href)).ToList();
		return new IDvachThreadParser.ThreadParserResult
		{
			ThreadUri = threadUri,
			MediaUris = mediaUris,
			ThreadId = threadId,
			ThreadName = threadName,
			ThreadStartTime = threadStartTime.Ticks == 0 ? null : threadStartTime,
		};
	}

	private static readonly IReadOnlySet<char> fileNameProhibitedChars = Path.GetInvalidFileNameChars().ToHashSet();
	private string? GetThreadName(IEnumerable<Func<string?>> candidatesFactory)
	{
		return candidatesFactory.Select(c => c()).Where(c => !string.IsNullOrWhiteSpace(c))?
			.Select(c => string.Concat(c!.Select(x => fileNameProhibitedChars.Contains(x) ? ' ' : x).Take(64)))
			.FirstOrDefault();
	}
}
