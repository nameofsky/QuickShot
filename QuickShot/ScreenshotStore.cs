using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickShot;

public static class ScreenshotStore
{
	public static event Action? Saved;

	public static void SaveCopies(Bitmap image, UserSettings settings, string applicationName)
	{
		if (!(settings.OutputMode == "CopyOnly") && !settings.OutputMode.Contains("仅复制"))
		{
			DateTime now = DateTime.Now;
			string day = now.ToString("yyyy-MM-dd");
			string archiveDirectory = Path.Combine(AppContext.BaseDirectory, "截图归类", day);
			string obj = (settings.UseDateFolderForExport ? Path.Combine(settings.SaveRoot, day) : settings.SaveRoot);
			Directory.CreateDirectory(archiveDirectory);
			Directory.CreateDirectory(obj);
			string cleanedName = CleanName(applicationName, "截图");
			string extension = settings.ImageFormat switch
			{
				"JPG" => "jpg",
				"BMP" => "bmp",
				"TIFF" => "tiff",
				"GIF" => "gif",
				_ => "png"
			};
			string archivePath = UniquePath(archiveDirectory, cleanedName, now, extension);
			string exportPath = UniquePath(obj, cleanedName, now, extension);
			ImageFormat format = extension switch
			{
				"jpg" => ImageFormat.Jpeg,
				"bmp" => ImageFormat.Bmp,
				"tiff" => ImageFormat.Tiff,
				"gif" => ImageFormat.Gif,
				_ => ImageFormat.Png
			};
			image.Save(archivePath, format);
			image.Save(exportPath, format);
			ScreenshotStore.Saved?.Invoke();
		}
	}

	public static void SaveGifCopies(IReadOnlyList<Bitmap> frames, UserSettings settings, string applicationName)
	{
		if (frames.Count == 0 || settings.OutputMode == "CopyOnly" || settings.OutputMode.Contains("仅复制"))
		{
			return;
		}

		DateTime now = DateTime.Now;
		string day = now.ToString("yyyy-MM-dd");
		string archiveDirectory = Path.Combine(AppContext.BaseDirectory, "截图归类", day);
		string exportDirectory = settings.UseDateFolderForExport ? Path.Combine(settings.SaveRoot, day) : settings.SaveRoot;
		Directory.CreateDirectory(archiveDirectory);
		Directory.CreateDirectory(exportDirectory);
		string name = CleanName(applicationName, "GIF录制");
		GifRecorder.Save(frames, UniquePath(archiveDirectory, name, now, "gif"), settings.GifFramerate);
		GifRecorder.Save(frames, UniquePath(exportDirectory, name, now, "gif"), settings.GifFramerate);
		Saved?.Invoke();
	}
	public static IReadOnlyList<ScreenshotItem> Recent(string root)
	{
		string archiveRoot = Path.Combine(AppContext.BaseDirectory, "截图归类");
		if (!Directory.Exists(archiveRoot))
		{
			return Array.Empty<ScreenshotItem>();
		}
		DateTime cutoff = DateTime.Today.AddDays(-29.0);
		return (from path in Directory.EnumerateFiles(archiveRoot, "*.png", SearchOption.AllDirectories)
			.Concat(Directory.EnumerateFiles(archiveRoot, "*.jpg", SearchOption.AllDirectories))
			.Concat(Directory.EnumerateFiles(archiveRoot, "*.bmp", SearchOption.AllDirectories))
			.Concat(Directory.EnumerateFiles(archiveRoot, "*.tiff", SearchOption.AllDirectories))
			.Concat(Directory.EnumerateFiles(archiveRoot, "*.gif", SearchOption.AllDirectories))
			select new FileInfo(path) into file
			where file.LastWriteTime >= cutoff
			orderby file.LastWriteTime descending
			select new ScreenshotItem(file.FullName, file.LastWriteTime.ToString("yyyy-MM-dd"), file.Name, LoadThumbnail(file.FullName))).ToList();
	}

	private static ImageSource? LoadThumbnail(string path)
	{
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(path);
			bitmapImage.DecodePixelWidth = 192;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	internal static string CleanName(string value, string fallback)
	{
		string cleaned = string.Concat(value.Select((char c) => (!Path.GetInvalidFileNameChars().Contains(c)) ? c : '_')).Trim('_');
		if (!string.IsNullOrWhiteSpace(cleaned))
		{
			return cleaned;
		}
		return fallback;
	}

	internal static string UniquePath(string directory, string name, DateTime timestamp, string extension)
	{
		string baseName = $"{name}_{timestamp:yyyy-MM-dd-HH-mm-ss}";
		string path = Path.Combine(directory, baseName + "." + extension);
		int suffix = 2;
		while (File.Exists(path))
		{
			path = Path.Combine(directory, $"{baseName}_{suffix++}.{extension}");
		}
		return path;
	}
}
