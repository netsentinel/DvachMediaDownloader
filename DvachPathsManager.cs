using System;
using System.Collections.Generic;
using System.Text;

namespace DvachMediaDownloader;

public interface IDvachPathsManager
{
	string GetWorkingDirectoryPath();
	string GetThreadWorkingDirectoryPath(string threadId, string? threadName, DateTime? threadStartedAt);
	string GetMediaFilePath(string threadWorkingDirectory, Uri mediaFileUri);
}

public interface IServicePathsManager
{
	public string GetLogFilePath();
}

public sealed class DvachPathsManager : IDvachPathsManager, IServicePathsManager
{
	public string GetWorkingDirectoryPath()
	{
		return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "DvachMediaDownloader");
	}

	public string GetThreadWorkingDirectoryPath(string threadId, string? threadName, DateTime? threadStartedAt)
	{
		if (Directory.Exists(GetWorkingDirectoryPath()) &&
			Directory.GetDirectories(GetWorkingDirectoryPath())
			.FirstOrDefault(x => x.StartsWith(threadId)) is string existingDirectory)
			return existingDirectory;

		var threadDirectoryName = threadId
			+ (threadStartedAt.HasValue ? $" {threadStartedAt.Value:yyyy-MM-dd}" : "")
			+ (string.IsNullOrWhiteSpace(threadName) ? "" : $" {threadName}");

		return Path.Join(GetWorkingDirectoryPath(), threadDirectoryName);
	}

	public string GetMediaFilePath(string threadWorkingDirectory, Uri mediaFileUri)
	{
		var fileName = mediaFileUri.OriginalString.Substring(mediaFileUri.OriginalString.LastIndexOf('/') + 1);
		return Path.Join(threadWorkingDirectory, fileName);
	}

	public string GetLogFilePath()
	{
		return Path.Join(GetWorkingDirectoryPath(), "app.log");
	}
}
