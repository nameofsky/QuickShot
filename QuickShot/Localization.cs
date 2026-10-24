using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickShot;

public static class Localization
{
	public const string DoNotLocalizeTag = "QuickShot.DoNotLocalize";

	private static readonly IReadOnlyDictionary<string, string> ChineseToEnglish = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["捕捉工作台"] = "Capture workspace",
		["截图与录屏"] = "Capture & recording",
		["开始捕捉"] = "Start capture",
		["最近截图"] = "Recent captures",
		["录制视频"] = "Video recording",
		["设置"] = "Settings",
		["语言"] = "Language",
		["已就绪"] = "Ready",
		["准备就绪"] = "Ready",
		["捕捉每一个瞬间"] = "Capture every moment",
		["移动到窗口上点击即可截图，也可以拖动选择任意区域。"] = "Click a window to capture it, or drag to select any area.",
		["开始截图"] = "Start capture",
		["开始录制"] = "Start recording",
		["只显示最近 30 天，历史图片不会自动删除"] = "Only the past 30 days are shown. Older images are kept.",
		["只显示最近 30 天的视频"] = "Only videos from the past 30 days are shown.",
		["打开截图文件夹"] = "Open capture folder",
		["打开视频文件夹"] = "Open video folder",
		["重命名"] = "Rename",
		["删除"] = "Delete",
		["双击打开视频"] = "Double-click to open a video",
		["截图与视频的保存、录制和显示方式"] = "Capture, recording, and display preferences",
		["截图"] = "Capture",
		["截图后的操作"] = "After capturing",
		["保存文件、复制到剪贴板，或同时执行"] = "Save the file, copy it to the clipboard, or do both",
		["保存并复制"] = "Save and copy",
		["仅保存"] = "Save only",
		["仅复制到剪贴板"] = "Copy to clipboard only",
		["图片格式"] = "Image format",
		["画质无损，JPG 文件更小"] = "Lossless quality; JPG files are smaller",
		["保存位置"] = "Save location",
		["截图会按日期自动建立文件夹"] = "Captures can be organized into folders by date",
		["选择"] = "Choose",
		["按日期分类保存"] = "Save by date",
		["按日期整理导出的文件"] = "Organize exported files by date",
		["启用日期文件夹"] = "Use date folders",
		["视频录制"] = "Video recording",
		["录制范围"] = "Recording area",
		["默认全屏；区域模式每次开始时重新框选"] = "Full screen by default; region mode asks you to select an area each time",
		["全屏"] = "Full screen",
		["指定区域"] = "Selected region",
		["记住上次区域"] = "Remember last region",
		["记住区域"] = "Remember region",
		["输出分辨率"] = "Output resolution",
		["原始分辨率保留采集区域像素"] = "Original resolution keeps the captured pixels",
		["原始分辨率"] = "Original resolution",
		["帧率"] = "Frame rate",
		["到 144 fps，越高越流畅，文件也越大"] = "Up to 144 fps. Higher values are smoother and produce larger files.",
		["码率"] = "Bitrate",
		["画质越高，文件也越大"] = "Higher quality produces larger files",
		["标准 8 Mbps"] = "Standard 8 Mbps",
		["高质量 12 Mbps"] = "High quality 12 Mbps",
		["极高质量 20 Mbps"] = "Ultra quality 20 Mbps",
		["视频保存位置"] = "Video save location",
		["录制视频保存到指定文件夹"] = "Save recorded videos to the selected folder",
		["录制完成后打开视频所在文件夹"] = "Open the video folder after recording",
		["新录制默认开启麦克风"] = "Enable microphone by default for new recordings",
		["麦克风设备"] = "Microphone device",
		["录制中不能更换设备，只能开关当前声音"] = "The device cannot be changed during recording; only its audio can be toggled",
		["视频录制快捷键"] = "Video recording shortcut",
		["截图快捷键"] = "Capture shortcut",
		["点击右侧输入框后按下新的组合键"] = "Click the box on the right, then press a new key combination",
		["界面"] = "Interface",
		["界面主题"] = "Theme",
		["选择 QuickShot 的显示风格"] = "Choose how QuickShot looks",
		["深色"] = "Dark",
		["浅色"] = "Light",
		["跟随系统"] = "Follow system",
		["文字识别快捷键"] = "Text recognition shortcut",
		["按下组合键以设置文字识别快捷键"] = "Press a key combination to set the text recognition shortcut",
		["按下 Ctrl、Shift 或 Alt 加任意按键以设置；按 Delete 清空"] = "Press Ctrl, Shift, or Alt with another key to set it; press Delete to clear",
		["快捷键至少需要 Ctrl、Shift 或 Alt"] = "A shortcut must include Ctrl, Shift, or Alt",
		["快捷键已清空"] = "Shortcut cleared",
		["快捷键不能重复"] = "Shortcut already in use",
		["截图、文字识别和视频录制必须使用不同的快捷键，请重新设置。"] = "Capture, text recognition, and video recording must use different shortcuts. Please choose another one.",
		["重新设置"] = "Set another shortcut",
		["该快捷键已被其他程序占用，已保留原设置"] = "This shortcut is used by another application. The previous setting was kept.",
		["快捷键已更新"] = "Shortcut updated",
		["简体中文"] = "Simplified Chinese",
		["选择 QuickShot 的显示语言"] = "Choose the display language for QuickShot",
		["打开 QuickShot"] = "Open QuickShot",
		["文字识别"] = "Text recognition",
		["退出"] = "Exit",
		["取消"] = "Cancel",
		["确认"] = "Confirm",
		["确定"] = "OK",
		["知道了"] = "OK",
		["拖动选择录制区域，Esc 取消"] = "Drag to select a recording area. Press Esc to cancel.",
		["重命名文件"] = "Rename file",
		["输入新名称；原文件扩展名会保留。"] = "Enter a new name. The original file extension will be preserved.",
		["录制控制"] = "Recording controls",
		["准备录制"] = "Ready to record",
		["正在录制"] = "Recording",
		["麦克风：关"] = "Microphone: Off",
		["麦克风：开"] = "Microphone: On",
		["麦克风：开启中"] = "Microphone: Enabling",
		["麦克风：关闭中"] = "Microphone: Disabling",
		["暂停"] = "Pause",
		["恢复"] = "Resume",
		["停止"] = "Stop",
		["无法开始录制："] = "Unable to start recording: ",
		["无法打开图片："] = "Unable to open image: ",
		["无法打开视频："] = "Unable to open video: ",
		["找不到要重命名的文件。"] = "The file to rename could not be found.",
		["目标文件名已经存在。"] = "A file with that name already exists.",
		["重命名失败："] = "Rename failed: ",
		["删除失败"] = "Delete failed",
		["视频"] = "Video",
		["确定删除“"] = "Delete \"",
		["”吗？此操作无法撤销。"] = "\"? This action cannot be undone.",
		["麦克风切换失败"] = "Microphone switch failed",
		["当前录制引擎未能切换麦克风状态。"] = "The recording engine could not change the microphone state.",
		["录制已经在进行中。"] = "Recording is already in progress.",
		["视频录制失败。"] = "Video recording failed.",
		["已识别并复制到剪贴板"] = "Text recognized and copied to the clipboard",
		["个字符"] = "characters",
		["识别成功，但剪贴板正被其他程序占用，未能复制。"] = "Text was recognized, but the clipboard is in use by another application.",
		["离线文字识别无法启动："] = "Offline text recognition could not start: ",
		["缺少 OCR 语言包。"] = "OCR language data is missing.",
		["未识别到可复制的文字，原剪贴板内容未改变。"] = "No copyable text was recognized. The clipboard was not changed.",
		["文字识别失败："] = "Text recognition failed: ",
		["未知错误"] = "Unknown error",
		["GIF 是否录制动图"] = "Record animated GIFs",
		["GIF 动图录制"] = "GIF recording",
		["框选后开始录制，再次点击屏幕结束"] = "Drag a region to start recording; click the screen to stop",
		["GIF 热键停止录制"] = "Stop GIF recording with a hotkey",
		["热键停止"] = "Hotkey stop",
		["开启后录制中点击不再结束，用下方热键停止录制"] = "When enabled, clicking no longer stops recording; stop with the hotkey below",
		["GIF 停止热键"] = "GIF stop hotkey",
		["按下组合键以设置 GIF 停止热键"] = "Press a key combination to set the GIF stop hotkey",
		["按下 Ctrl、Shift 或 Alt 加任意按键以设置；按 Delete 恢复默认（Ctrl + Shift + G）"] = "Press Ctrl, Shift, or Alt with another key to set it; press Delete to restore the default (Ctrl + Shift + G)",
		["快捷键至少需要 Ctrl、Shift 或 Alt（Tab 不能作为修饰键）"] = "A shortcut must include Ctrl, Shift, or Alt (Tab cannot be a modifier key)",
		["已恢复默认热键"] = "Default hotkey restored",
		["已设置为 "] = "Set to ",
		["GIF 动图每秒帧数"] = "GIF frames per second",
		["范围 1-25 fps"] = "Range 1-25 fps",
		["停止热键「"] = "The stop hotkey \"",
		["」注册失败（可能被其他软件占用，或笔记本 F 键默认为媒体键），本次录制仍为点击屏幕结束。"] = "\" could not be registered (it may be in use by another app, or laptop F-keys may default to media functions). This recording still stops when you click the screen.",
		["背景"] = "Background",
		["自定义背景"] = "Custom background",
		["选择文件"] = "Choose file",
		["选择自定义背景"] = "Choose a custom background",
		["界面磨砂程度"] = "Interface blur amount",
		["范围 0-100%，默认 25%"] = "Range 0-100%; 25% by default",
		["当前：未选择"] = "Current: none selected",
		["当前："] = "Current: ",
		["请选择支持的图片或视频文件。"] = "Please choose a supported image or video file.",
		["无法设置自定义背景："] = "Could not set a custom background: ",
		["选择图片或视频，文件将复制到背景目录"] = "Choose an image or video; the file will be copied to the backgrounds folder",
		["图片和视频"] = "Images and videos",
		["图片"] = "Images",
		["删除截图"] = "Delete capture",
		["删除视频"] = "Delete video",
		["区域截图"] = "Region capture",
		["QuickShot 启动失败。\n\n错误日志："] = "QuickShot failed to start.\n\nError log: "
	};

	private static bool _isEnglish;

	private sealed class LocalizedValue
	{
		public string Source { get; set; } = string.Empty;

		public string Rendered { get; set; } = string.Empty;
	}

	private sealed class LocalizedNode
	{
		public Dictionary<string, LocalizedValue> Values { get; } = new(StringComparer.Ordinal);
	}

	// UI elements are reused between language changes. Keep their canonical Chinese
	// source instead of translating the text that a previous switch already rendered.
	private static readonly ConditionalWeakTable<DependencyObject, LocalizedNode> LocalizedNodes = new();

	public static event Action? LanguageChanged;

	public static bool IsEnglish => _isEnglish;

	public static void SetLanguage(string? language)
	{
		bool isEnglish = string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase);
		if (_isEnglish == isEnglish)
		{
			return;
		}

		_isEnglish = isEnglish;
		LanguageChanged?.Invoke();
	}

	public static string OcrResultSummary(string engine, long elapsedMilliseconds, int characterCount)
	{
		return _isEnglish
			? $"Text recognized and copied to clipboard ({engine}, {elapsedMilliseconds / 1000d:0.0}s, {characterCount} characters)"
			: $"\u5df2\u8bc6\u522b\u5e76\u590d\u5236\u5230\u526a\u8d34\u677f\uff08{engine}\uff0c{elapsedMilliseconds / 1000d:0.0}\u79d2\uff0c{characterCount}\u4e2a\u5b57\u7b26\uff09";
	}

	public static string Text(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		if (_isEnglish)
		{
			foreach ((string chinese, string english) in ChineseToEnglish.OrderByDescending(pair => pair.Key.Length))
			{
				value = value.Replace(chinese, english, StringComparison.Ordinal);
			}
			return value;
		}

		foreach ((string chinese, string english) in ChineseToEnglish.OrderByDescending(pair => pair.Value.Length))
		{
			value = value.Replace(english, chinese, StringComparison.Ordinal);
		}
		return value;
	}

	private static string CanonicalText(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		foreach ((string chinese, string english) in ChineseToEnglish.OrderByDescending(pair => pair.Value.Length))
		{
			value = value.Replace(english, chinese, StringComparison.Ordinal);
		}
		return value;
	}

	public static string Chinese(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		foreach ((string chinese, string english) in ChineseToEnglish)
			{
				if (string.Equals(english, value, StringComparison.Ordinal))
				{
					return chinese;
				}
			}
		return value;
	}

	public static void Apply(DependencyObject root)
	{
		Apply(root, new HashSet<DependencyObject>());
	}

	private static void Apply(DependencyObject root, ISet<DependencyObject> visited)
	{
		if (!visited.Add(root))
		{
			return;
		}

		if (root is FrameworkElement markerElement &&
			string.Equals(markerElement.Tag as string, DoNotLocalizeTag, StringComparison.Ordinal))
		{
			return;
		}

		ApplyNode(root);
		if (root is Visual or System.Windows.Media.Media3D.Visual3D)
		{
			for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
			{
				Apply(VisualTreeHelper.GetChild(root, index), visited);
			}
		}
		if (root is FrameworkElement or FrameworkContentElement)
		{
			foreach (object child in LogicalTreeHelper.GetChildren(root))
			{
				if (child is DependencyObject childNode)
				{
					Apply(childNode, visited);
				}
			}
		}
	}

	private static void ApplyNode(DependencyObject node)
	{
		if (node is TextBlock textBlock)
		{
			textBlock.Text = ApplyValue(node, "Text", textBlock.Text);
		}
		if (node is Run run)
		{
			run.Text = ApplyValue(node, "Text", run.Text);
		}
		if (node is ContentControl contentControl && contentControl.Content is string content)
		{
			contentControl.Content = ApplyValue(node, "Content", content);
		}
		if (node is HeaderedContentControl headeredContent && headeredContent.Header is string header)
		{
			headeredContent.Header = ApplyValue(node, "Header", header);
		}
		if (node is FrameworkElement element)
		{
			if (element.ToolTip is string tooltip)
			{
				element.ToolTip = ApplyValue(node, "ToolTip", tooltip);
			}
			if (element.ContextMenu != null)
			{
				ApplyMenu(element.ContextMenu.Items);
			}
		}
	}

	private static string ApplyValue(DependencyObject node, string slot, string currentValue)
	{
		LocalizedNode localizedNode = LocalizedNodes.GetOrCreateValue(node);
		if (!localizedNode.Values.TryGetValue(slot, out LocalizedValue? value))
		{
			value = new LocalizedValue { Source = CanonicalText(currentValue) };
			localizedNode.Values.Add(slot, value);
		}
		else if (!string.Equals(currentValue, value.Rendered, StringComparison.Ordinal))
		{
			// A status label changed outside localization. Treat that new value as
			// fresh source text, while normal language rerenders keep their source.
			value.Source = CanonicalText(currentValue);
		}

		value.Rendered = Text(value.Source);
		return value.Rendered;
	}

	private static void ApplyMenu(ItemCollection items)
	{
		foreach (object item in items)
		{
			if (item is MenuItem menuItem)
			{
				if (menuItem.Header is string header)
				{
					menuItem.Header = ApplyValue(menuItem, "Header", header);
				}
				ApplyMenu(menuItem.Items);
			}
		}
	}
}
