using System;
using System.IO;

namespace QuickShot;

public sealed class UserSettings
{
	public string Language { get; set; } = "zh-CN";

	public string SaveRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "截图");

	public string OutputMode { get; set; } = "SaveAndCopy";

	public string ImageFormat { get; set; } = "PNG";

	public bool GifRecordingEnabled { get; set; }

	public int GifFramerate { get; set; } = 10;

	public string BackgroundPath { get; set; } = "";

	public int BackgroundBlurPercent { get; set; } = 25;

	public string Theme { get; set; } = "Dark";

	public string Shortcut { get; set; } = "Ctrl + Shift + A";

	public string VideoShortcut { get; set; } = "";

	public string OcrShortcut { get; set; } = "Ctrl + Shift + O";

	public bool UseDateFolderForExport { get; set; }

	public string VideoSaveRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "录制视频");

	public string VideoCaptureMode { get; set; } = "全屏";

	public bool RememberVideoRegion { get; set; }

	public int VideoRegionLeft { get; set; }

	public int VideoRegionTop { get; set; }

	public int VideoRegionWidth { get; set; }

	public int VideoRegionHeight { get; set; }

	public int VideoWidth { get; set; }

	public int VideoHeight { get; set; }

	public int VideoFramerate { get; set; } = 30;

	public int VideoBitrateKbps { get; set; } = 12000;

	public bool MicrophoneEnabledByDefault { get; set; }

	public string MicrophoneDevice { get; set; } = "";

	public bool OpenVideoFolderAfterRecording { get; set; } = true;
}
