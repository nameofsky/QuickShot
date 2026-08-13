using System;
using System.IO;
using System.Text.Json;

namespace QuickShot;

public static class SettingsStore
{
	private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShot", "settings.json");

	private static readonly string DefaultVideoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "录制视频"));

	public static UserSettings Load()
	{
		try
		{
			UserSettings settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new UserSettings();
			if (UsesLegacyDefaultVideoRoot(settings.VideoSaveRoot))
			{
				settings.VideoSaveRoot = DefaultVideoRoot;
				Save(settings);
			}
			EnsureDefaultVideoRoot(settings);
			return settings;
		}
		catch
		{
			UserSettings settings = new UserSettings { VideoSaveRoot = DefaultVideoRoot };
			EnsureDefaultVideoRoot(settings);
			return settings;
		}
	}

	public static void Save(UserSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
		File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	private static bool UsesLegacyDefaultVideoRoot(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return true;
		}

		string fullPath;
		try
		{
			fullPath = Path.GetFullPath(path);
		}
		catch
		{
			return false;
		}

		if (string.Equals(fullPath, DefaultVideoRoot, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		DirectoryInfo? parent = Directory.GetParent(fullPath);
		return string.Equals(Path.GetFileName(fullPath), "录制视频", StringComparison.OrdinalIgnoreCase)
			&& (fullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
				|| (parent?.Name.StartsWith("QuickShot", StringComparison.OrdinalIgnoreCase) ?? false));
	}

	private static void EnsureDefaultVideoRoot(UserSettings settings)
	{
		if (string.Equals(Path.GetFullPath(settings.VideoSaveRoot), DefaultVideoRoot, StringComparison.OrdinalIgnoreCase))
		{
			Directory.CreateDirectory(DefaultVideoRoot);
		}
	}
}
