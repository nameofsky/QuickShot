using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuickShot;

public static class VideoStore
{
	public static string Root(UserSettings settings)
	{
		if (!string.IsNullOrWhiteSpace(settings.VideoSaveRoot))
		{
			return settings.VideoSaveRoot;
		}
		return Path.Combine(AppContext.BaseDirectory, "录制视频");
	}

	public static string CreatePath(UserSettings settings)
	{
		DateTime now = DateTime.Now;
		string text = Path.Combine(Root(settings), now.ToString("yyyy-MM-dd-HH"));
		Directory.CreateDirectory(text);
		return ScreenshotStore.UniquePath(text, "QuickShot", now, "mp4");
	}

	public static IReadOnlyList<VideoItem> Recent(string root)
	{
		if (!Directory.Exists(root))
		{
			return Array.Empty<VideoItem>();
		}
		DateTime cutoff = DateTime.Today.AddDays(-29.0);
		return (from path in Directory.EnumerateFiles(root, "*.mp4", SearchOption.AllDirectories)
			select new FileInfo(path) into file
			where file.LastWriteTime >= cutoff
			orderby file.LastWriteTime descending
			select new VideoItem(file.FullName, file.LastWriteTime.ToString("yyyy-MM-dd"), file.Name)).ToList();
	}
}
